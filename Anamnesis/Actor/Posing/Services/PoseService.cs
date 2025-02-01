﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor;

using Anamnesis.Core.Memory;
using Anamnesis.Files;
using Anamnesis.Memory;
using Anamnesis.Services;
using PropertyChanged;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

[AddINotifyPropertyChangedInterface]
public class PoseService : ServiceBase<PoseService>
{
	private NopHook? freezeRot1;
	private NopHook? freezeRot2;
	private NopHook? freezeRot3;
	private NopHook? freezeScale1;
	private NopHook? freezePosition;
	private NopHook? freezePosition2;
	private NopHook? freeseScale2;
	private NopHook? freezePhysics1;
	private NopHook? freezePhysics2;
	private NopHook? freezePhysics3;
	private NopHook? freezeWorldPosition;
	private NopHook? freezeWorldRotation;
	private NopHook? freezeGposeTargetPosition;

	private bool isEnabled;

	public delegate void PoseEvent(bool value);

	public static event PoseEvent? EnabledChanged;
	public static event PoseEvent? FreezeWorldPositionsEnabledChanged;

	public static string? SelectedBonesText { get; set; }

	public bool IsEnabled
	{
		get
		{
			return this.isEnabled;
		}

		set
		{
			if (this.IsEnabled == value)
				return;

			this.SetEnabled(value);
		}
	}

	public bool FreezePhysics
	{
		get
		{
			return this.freezePhysics1?.Enabled ?? false;
		}
		set
		{
			this.freezePhysics1?.SetEnabled(value);
			this.freezePhysics2?.SetEnabled(value);
		}
	}

	public bool FreezePositions
	{
		get
		{
			return this.freezePosition?.Enabled ?? false;
		}
		set
		{
			this.freezePosition?.SetEnabled(value);
			this.freezePosition2?.SetEnabled(value);
		}
	}

	public bool FreezeScale
	{
		get
		{
			return this.freezeScale1?.Enabled ?? false;
		}
		set
		{
			this.freezeScale1?.SetEnabled(value);
			this.freezePhysics3?.SetEnabled(value);
			this.freeseScale2?.SetEnabled(value);
		}
	}

	public bool FreezeRotation
	{
		get
		{
			return this.freezeRot1?.Enabled ?? false;
		}
		set
		{
			this.freezeRot1?.SetEnabled(value);
			this.freezeRot2?.SetEnabled(value);
			this.freezeRot3?.SetEnabled(value);
		}
	}

	public bool WorldPositionNotFrozen => !this.FreezeWorldPosition;

	public bool FreezeWorldPosition
	{
		get
		{
			return this.freezeWorldPosition?.Enabled ?? false;
		}
		set
		{
			this.freezeWorldPosition?.SetEnabled(value);
			this.freezeWorldRotation?.SetEnabled(value);
			this.freezeGposeTargetPosition?.SetEnabled(value);
			this.RaisePropertyChanged(nameof(PoseService.FreezeWorldPosition));
			this.RaisePropertyChanged(nameof(PoseService.WorldPositionNotFrozen));
			FreezeWorldPositionsEnabledChanged?.Invoke(this.IsEnabled);
		}
	}

	public bool EnableParenting { get; set; } = true;

	public bool CanEdit { get; set; }

	public override async Task Initialize()
	{
		await base.Initialize();

		this.freezePosition = new NopHook(AddressService.SkeletonFreezePosition, 5);
		this.freezePosition2 = new NopHook(AddressService.SkeletonFreezePosition2, 5);
		this.freezeRot1 = new NopHook(AddressService.SkeletonFreezeRotation, 6);
		this.freezeRot2 = new NopHook(AddressService.SkeletonFreezeRotation2, 6);
		this.freezeRot3 = new NopHook(AddressService.SkeletonFreezeRotation3, 4);
		this.freezeScale1 = new NopHook(AddressService.SkeletonFreezeScale, 6);
		this.freeseScale2 = new NopHook(AddressService.SkeletonFreezeScale2, 6);
		this.freezePhysics1 = new NopHook(AddressService.SkeletonFreezePhysics, 4);
		this.freezePhysics2 = new NopHook(AddressService.SkeletonFreezePhysics2, 3);
		this.freezePhysics3 = new NopHook(AddressService.SkeletonFreezePhysics3, 4);
		this.freezeWorldPosition = new NopHook(AddressService.WorldPositionFreeze, 16);
		this.freezeWorldRotation = new NopHook(AddressService.WorldRotationFreeze, 4);
		this.freezeGposeTargetPosition = new NopHook(AddressService.GPoseCameraTargetPositionFreeze, 5);

		GposeService.GposeStateChanged += this.OnGposeStateChanged;

		_ = Task.Run(ExtractStandardPoses);
	}

	public override async Task Shutdown()
	{
		await base.Shutdown();
		this.SetEnabled(false);
		this.FreezeWorldPosition = false;
	}

	public void SetEnabled(bool enabled)
	{
		// Don't try to enable posing unless we are in gpose
		if (enabled && !GposeService.Instance.IsGpose)
			throw new Exception("Attempt to enable posing outside of gpose");

		if (this.isEnabled == enabled)
			return;

		this.isEnabled = enabled;
		this.FreezePhysics = enabled;
		this.FreezeRotation = enabled;
		this.FreezePositions = enabled;
		this.FreezeScale = false;
		this.EnableParenting = true;

		/*if (enabled)
		{
			this.FreezeWorldPosition = true;
			AnimationService.Instance.PausePinnedActors();
		}*/

		EnabledChanged?.Invoke(enabled);

		this.RaisePropertyChanged(nameof(this.IsEnabled));
	}

	private static async Task ExtractStandardPoses()
	{
		try
		{
			DirectoryInfo standardPoseDir = FileService.StandardPoseDirectory.Directory;
			string verFile = standardPoseDir.FullName + "\\ver.txt";

			if (standardPoseDir.Exists)
			{
				if (File.Exists(verFile))
				{
					try
					{
						string verText = await File.ReadAllTextAsync(verFile);
						DateTime standardPoseVersion = DateTime.Parse(verText, CultureInfo.InvariantCulture);

						if (standardPoseVersion == VersionInfo.Date)
						{
							Log.Information($"Standard pose library up to date");
							return;
						}
					}
					catch (Exception ex)
					{
						Log.Warning(ex, "Failed to read standard pose library version file");
					}
				}

				standardPoseDir.Delete(true);
			}

			standardPoseDir.Create();
			await File.WriteAllTextAsync(verFile, VersionInfo.Date.ToString(CultureInfo.InvariantCulture));

			string[] poses = EmbeddedFileUtility.GetAllFilesInDirectory("\\Data\\StandardPoses\\");
			foreach (string posePath in poses)
			{
				string destPath = posePath;
				destPath = destPath.Replace('.', '\\');
				destPath = destPath.Replace('_', ' ');
				destPath = destPath.Replace("Data\\StandardPoses\\", string.Empty);

				// restore file extensions
				destPath = destPath.Replace("\\pose", ".pose");
				destPath = destPath.Replace("\\txt", ".txt");

				destPath = standardPoseDir.FullName + destPath;

				string? destDir = Path.GetDirectoryName(destPath);

				if (destDir == null)
					throw new Exception($"Failed to get directory name from path: {destPath}");

				if (!Directory.Exists(destDir))
					Directory.CreateDirectory(destDir);

				using Stream contents = EmbeddedFileUtility.Load(posePath);
				using FileStream fileStream = new FileStream(destPath, FileMode.Create);
				await contents.CopyToAsync(fileStream);
			}

			Log.Information($"Extracted standard pose library");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to extract standard pose library");
		}
	}

	private void OnGposeStateChanged(bool isGPose)
	{
		if (!isGPose)
		{
			this.SetEnabled(false);
			this.FreezeWorldPosition = false;
		}
	}
}
