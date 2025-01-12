﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Views;

using Anamnesis.Actor.Posing;
using Anamnesis.Actor.Posing.Visuals;
using Anamnesis.Services;
using PropertyChanged;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using XivToolsWpf;
using XivToolsWpf.Math3D.Extensions;

// TODO: Allow the user to rotate camera with the mouse

/// <summary>
/// Interaction logic for Pose3DView.xaml.
/// </summary>
[AddINotifyPropertyChangedInterface]
public partial class Pose3DView : UserControl
{
	public readonly PerspectiveCamera Camera;
	public readonly RotateTransform3D CameraRotation;
	public readonly TranslateTransform3D CameraPosition;

	private CancellationTokenSource? camUpdateCancelTokenSrc;
	private bool cameraIsTicking = false;

	private Point lastMousePosition;
	private bool isPanning = false;

	public Pose3DView()
	{
		this.InitializeComponent();

		this.Camera = new PerspectiveCamera(new Point3D(0, 0.75, -4), new Vector3D(0, 0, 1), new Vector3D(0, 1, 0), 45);
		this.Viewport.Camera = this.Camera;

		this.CameraRotation = new RotateTransform3D();
		QuaternionRotation3D camRot = new()
		{
			Quaternion = CameraService.Instance.Camera?.Rotation3d.ToMedia3DQuaternion() ?? Quaternion.Identity,
		};
		this.CameraRotation.Rotation = camRot;
		this.CameraPosition = new TranslateTransform3D();

		Transform3DGroup transformGroup = new();
		transformGroup.Children.Add(this.CameraRotation);
		transformGroup.Children.Add(this.CameraPosition);
		this.Camera.Transform = transformGroup;

		this.ContentArea.DataContext = this;
	}

	public SkeletonEntity? Skeleton { get; set; }
	public SkeletonVisual3D? Visual { get; set; }

	public double CameraDistance { get; set; }

	public string BoneSearch { get; set; } = string.Empty;
	public IEnumerable<BoneEntity> BoneSearchResult
	{
		get
		{
			if (this.Skeleton == null)
				return Array.Empty<BoneEntity>();

			return string.IsNullOrWhiteSpace(this.BoneSearch)
				? this.Skeleton.Bones.Values.OfType<BoneEntity>()
				: this.Skeleton.Bones.Values.OfType<BoneEntity>().Where(b => FileSystemName.MatchesSimpleExpression($"*{this.BoneSearch}*", b.Name) || FileSystemName.MatchesSimpleExpression($"*{this.BoneSearch}*", b.Tooltip));
		}
	}

	private static BoneVisual3D? FindBoneVisual(DependencyObject visual)
	{
		while (visual != null)
		{
			if (visual is BoneVisual3D boneVisual)
				return boneVisual;

			visual = VisualTreeHelper.GetParent(visual);
		}

		return null;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		this.OnDataContextChanged(null, default);
		Task.Run(this.UpdateCamera);
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		this.camUpdateCancelTokenSrc?.Cancel();
		this.SkeletonRoot.Children.Clear();
	}

	private void OnDataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
	{
		if (this.DataContext is not SkeletonEntity skeleton)
			return;

		this.Skeleton = skeleton;
		this.SkeletonRoot.Children.Clear();

		this.Visual = new SkeletonVisual3D(this.Skeleton);

		if (!this.SkeletonRoot.Children.Contains(this.Visual))
			this.SkeletonRoot.Children.Add(this.Visual);

		this.SkeletonRoot.Children.Add(new ModelVisual3D() { Content = new AmbientLight(Colors.White) });

		this.FrameSkeleton();
	}

	private void OnFrameClicked(object sender, RoutedEventArgs e)
	{
		this.FrameSkeleton();
	}

	private void FrameSkeleton()
	{
		// Position camera at average center position of skeleton
		if (this.Skeleton == null || this.Skeleton.Bones == null || this.Skeleton.Bones.IsEmpty)
			return;

		Rect3D bounds = default;
		foreach (var bone in this.Skeleton.Bones.Values.OfType<BoneEntity>())
		{
			bounds.Union(new Point3D(bone.Position.X, bone.Position.Y, bone.Position.Z));
		}

		this.CameraDistance = Math.Max(Math.Max(bounds.SizeX, bounds.SizeY), bounds.SizeZ);
	}

	private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left)
		{
			Point mousePosition = e.GetPosition(this.Viewport);
			HitTestResult hitResult = VisualTreeHelper.HitTest(this.Viewport, mousePosition);

			if (hitResult is RayHitTestResult rayHitResult)
			{
				BoneVisual3D? boneVisual = FindBoneVisual(rayHitResult.VisualHit);
				if (boneVisual != null)
				{
					this.Skeleton?.Select(boneVisual.Bone);
					e.Handled = true;
				}
			}
		}
		else if (e.ChangedButton == MouseButton.Middle && Keyboard.IsKeyDown(Key.LeftShift))
		{
			this.isPanning = true;
			this.lastMousePosition = e.GetPosition(this.Viewport);
			this.Viewport.CaptureMouse();
			e.Handled = true;
		}
	}

	private void OnViewportMouseMove(object sender, MouseEventArgs e)
	{
		if (this.isPanning)
		{
			Point currentMousePosition = e.GetPosition(this.Viewport);
			Vector delta = Point.Subtract(currentMousePosition, this.lastMousePosition);

			double panSpeed = 0.005;
			this.CameraPosition.OffsetX -= delta.X * panSpeed;
			this.CameraPosition.OffsetY += delta.Y * panSpeed;

			this.lastMousePosition = currentMousePosition;
			e.Handled = true;
		}
	}

	private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Middle)
		{
			this.isPanning = false;
			this.Viewport.ReleaseMouseCapture();
			e.Handled = true;
		}
	}

	private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
	{
		this.CameraDistance -= e.Delta / 120;
		this.CameraDistance = Math.Clamp(this.CameraDistance, 0, 300);
		e.Handled = true;
	}

	private async Task UpdateCamera()
	{
		if (this.cameraIsTicking)
			return;

		this.cameraIsTicking = true;
		this.camUpdateCancelTokenSrc = new CancellationTokenSource();
		var token = this.camUpdateCancelTokenSrc.Token;

		await Dispatch.MainThread();

		while (this.IsLoaded)
		{
			// If we're not in GPose or the view is not visible, skip the update
			if (!this.IsVisible || !GposeService.Instance.IsGpose)
			{
				await Task.Delay(100, token);
				continue;
			}

			await Task.Delay(33, token);
			await Dispatch.MainThread();

			try
			{
				// Validate that all objects are valid and we're in GPose
				if (!GposeService.Instance.IsGpose || this.Skeleton == null || this.Skeleton.Actor == null || CameraService.Instance.Camera == null)
					continue;

				// Update visual skeleton
				this.Visual?.Update();

				// Apply camera rotation
				QuaternionRotation3D rot = (QuaternionRotation3D)this.CameraRotation.Rotation;
				rot.Quaternion = CameraService.Instance.Camera.Rotation3d.ToMedia3DQuaternion();
				this.CameraRotation.Rotation = rot;

				// Apply camera position
				Point3D pos = this.Camera.Position;
				pos.Z = -this.CameraDistance;
				this.Camera.Position = pos;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to update pose camera");
			}
		}

		this.cameraIsTicking = false;
	}
}
