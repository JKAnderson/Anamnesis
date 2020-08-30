﻿// Concept Matrix 3.
// Licensed under the MIT license.

namespace Anamnesis.GUI.Views
{
	using System;
	using System.Collections.ObjectModel;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using Anamnesis.GameData;
	using Anamnesis.Memory;
	using PropertyChanged;

	using Actor = Anamnesis.Actor;
	using Vector = Anamnesis.Memory.Vector;

	/// <summary>
	/// Interaction logic for WorldView.xaml.
	/// </summary>
	[AddINotifyPropertyChangedInterface]
	[SuppressPropertyChangedWarnings]
	public partial class HomeView : UserControl
	{
		private IGameDataService gameData;
		private IMarshaler<int>? territoryMem;
		private IMarshaler<ushort>? weatherMem;
		private IMarshaler<Vector2D>? cameraAngleMem;
		private IMarshaler<Vector2D>? cameraPanMem;
		private IMarshaler<float>? cameraRotatonMem;
		private IMarshaler<float>? cameraZoomMem;
		private IMarshaler<float>? cameraFovMem;
		private IMarshaler<Vector>? cameraPositionMem;
		private IMarshaler<float>? cameraMinZoomMem;
		private IMarshaler<float>? cameraMaxZoomMem;
		private IMarshaler<Vector>? posMem;
		private IMarshaler<Quaternion>? rotMem;
		private IMarshaler<Vector>? scaleMem;

		private bool isGpose;
		private bool initialized = false;

		public HomeView()
		{
			this.InitializeComponent();

			this.gameData = Anamnesis.Services.Get<IGameDataService>();

			this.TimeService = Anamnesis.Services.Get<TimeService>();

			this.ContentArea.DataContext = this;
		}

		public TimeService TimeService { get; private set; }
		public string Territory { get; set; } = "Unknown";

		public float CameraAngleX
		{
			get => this.CameraAngle.X;
			set
			{
				this.CameraAngle = new Vector2D(value, this.CameraAngleY);
				this.cameraAngleMem?.SetValue(this.CameraAngle);
			}
		}

		public float CameraAngleY
		{
			get => this.CameraAngle.Y;
			set
			{
				this.CameraAngle = new Vector2D(this.CameraAngleX, value);
				this.cameraAngleMem?.SetValue(this.CameraAngle);
			}
		}

		public bool LockCameraAngle { get; set; }
		public Vector2D CameraAngle { get; set; }
		public Vector2D CameraPan { get; set; }
		public Vector CameraPosition { get; set; }
		public bool LockCameraPosition { get; set; }
		public float CameraRotaton { get; set; }
		public float CameraZoom { get; set; }
		public float CameraMinZoom { get; private set; }
		public float CameraMaxZoom { get; private set; }
		public float CameraFov { get; set; }
		public Vector Position { get; set; }
		public Quaternion Rotation { get; set; }
		public Vector Scale { get; set; }

		public bool IsGpose
		{
			get
			{
				return this.isGpose;
			}

			set
			{
				this.isGpose = value;

				if (this.isGpose)
				{
					this.cameraAngleMem = MemoryService.GetMarshaler(Offsets.Main.CameraAddress, Offsets.Main.CameraAngle);
					this.cameraAngleMem.ValueChanged += this.OnCameraAngleMemValueChanged;

					this.cameraPanMem = MemoryService.GetMarshaler(Offsets.Main.CameraAddress, Offsets.Main.CameraPan);
					this.cameraPanMem.Bind(this, nameof(this.CameraPan));

					this.cameraRotatonMem = MemoryService.GetMarshaler(Offsets.Main.CameraAddress, Offsets.Main.CameraRotation);
					this.cameraRotatonMem.Bind(this, nameof(this.CameraRotaton));

					this.cameraZoomMem = MemoryService.GetMarshaler(Offsets.Main.CameraAddress, Offsets.Main.CameraCurrentZoom);
					this.cameraZoomMem.Bind(this, nameof(this.CameraZoom));

					this.cameraMinZoomMem = MemoryService.GetMarshaler(Offsets.Main.CameraAddress, Offsets.Main.CameraMinZoom);
					this.cameraMinZoomMem.Bind(this, nameof(this.CameraMinZoom));

					this.cameraMaxZoomMem = MemoryService.GetMarshaler(Offsets.Main.CameraAddress, Offsets.Main.CameraMaxZoom);
					this.cameraMaxZoomMem.Bind(this, nameof(this.CameraMaxZoom));

					this.cameraFovMem = MemoryService.GetMarshaler(Offsets.Main.CameraAddress, Offsets.Main.FOVCurrent);
					this.cameraFovMem.Bind(this, nameof(this.CameraFov));

					this.cameraPositionMem = MemoryService.GetMarshaler(Offsets.Main.Gpose, Offsets.Main.Camera);
					this.cameraPositionMem.Bind(this, nameof(this.CameraPosition));

					if (this.territoryMem != null && this.territoryMem.Active)
					{
						this.OnTerritoryMemValueChanged(null, 0);
					}
				}
				else
				{
					this.cameraAngleMem?.Dispose();
					this.cameraPanMem?.Dispose();
					this.cameraRotatonMem?.Dispose();
					this.cameraZoomMem?.Dispose();
					this.cameraFovMem?.Dispose();
					this.cameraPositionMem?.Dispose();
					this.cameraMinZoomMem?.Dispose();
					this.cameraMaxZoomMem?.Dispose();
				}
			}
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			TargetService.ModeChanged += this.OnSelectionServiceModeChanged;

			this.initialized = false;

			this.SetActor(this.DataContext as Actor);
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.SetActor(this.DataContext as Actor);
		}

		private void OnSelectionServiceModeChanged(Modes mode)
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				this.IsGpose = mode == Modes.GPose;
			});
		}

		private void OnUnloaded(object sender, RoutedEventArgs e)
		{
			this.IsGpose = false;

			this.SetActor(null);

			this.territoryMem?.Dispose();
			this.weatherMem?.Dispose();
		}

		private void OnCameraAngleMemValueChanged(object? sender, Vector2D value)
		{
			if (this.LockCameraAngle)
			{
				this.cameraAngleMem?.SetValue(this.CameraAngle);
			}
			else if (this.cameraAngleMem != null)
			{
				this.CameraAngle = this.cameraAngleMem.Value;
			}
		}

		private void OnTerritoryMemValueChanged(object? sender = null, int value = 0)
		{
			if (this.territoryMem == null || this.weatherMem == null)
				return;

			int territoryId = this.territoryMem.Value;
			ushort currentWeather = this.weatherMem.Value;

			ITerritoryType territory = this.gameData.Territories.Get(territoryId);

			if (territory == null)
			{
				this.Territory = "Unknwon";

				Application.Current.Dispatcher.Invoke(() =>
				{
					this.WeatherComboBox.ItemsSource = null;
				});
			}
			else
			{
				this.Territory = territory.Region + " - " + territory.Place;

				Application.Current.Dispatcher.Invoke(() =>
				{
					this.WeatherComboBox.ItemsSource = territory.Weathers;

					foreach (IWeather weather in territory.Weathers)
					{
						byte[] bytes = { (byte)weather.Key, (byte)weather.Key };
						ushort weatherVal = BitConverter.ToUInt16(bytes, 0);

						if (weatherVal == currentWeather)
						{
							this.WeatherComboBox.SelectedItem = weather;
						}
					}
				});
			}
		}

		private void OnWeatherSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			IWeather? weather = this.WeatherComboBox.SelectedItem as IWeather;

			if (weather == null)
				return;

			// This is super weird. I have no idea why we need to do this for weather...
			byte[] bytes = { (byte)weather.Key, (byte)weather.Key };
			this.weatherMem?.SetValue(BitConverter.ToUInt16(bytes, 0));
		}

		private void OnUnlockCameraChanged(object sender, RoutedEventArgs e)
		{
			if (this.UnlockCameraCheckbox.IsChecked == null)
				this.UnlockCameraCheckbox.IsChecked = false;

			bool unlock = (bool)this.UnlockCameraCheckbox.IsChecked;

			this.CameraMaxZoom = unlock ? 256 : 20;
			this.CameraMinZoom = unlock ? 0 : 1.75f;

			using IMarshaler<float> minYMem = MemoryService.GetMarshaler(Offsets.Main.CameraAddress, Offsets.Main.CameraYMin);
			minYMem.Value = unlock ? 1.5f : 1.25f;

			using IMarshaler<float> maxYMem = MemoryService.GetMarshaler(Offsets.Main.CameraAddress, Offsets.Main.CameraYMax);
			maxYMem.Value = unlock ? -1.5f : -1.4f;
		}

		private void SetActor(Actor? actor)
		{
			this.cameraPositionMem?.Dispose();
			this.posMem?.Dispose();
			this.rotMem?.Dispose();
			this.scaleMem?.Dispose();

			if (actor == null)
				return;

			if (!this.initialized)
			{
				this.initialized = true;

				this.weatherMem = MemoryService.GetMarshaler(Offsets.Main.GposeFilters, Offsets.Main.ForceWeather);
				this.territoryMem = null;
				this.territoryMem = MemoryService.GetMarshaler(Offsets.Main.TerritoryAddress, Offsets.Main.Territory);
				this.territoryMem.ValueChanged += this.OnTerritoryMemValueChanged;
				this.OnTerritoryMemValueChanged(null, 0);

				this.IsGpose = TargetService.CurrentMode == Modes.GPose;
			}

			this.posMem = actor.GetMemory(Offsets.Main.Position);
			this.posMem.Bind(this, nameof(this.Position));

			this.rotMem = actor.GetMemory(Offsets.Main.Rotation);
			this.rotMem.Bind(this, nameof(this.Rotation));

			this.scaleMem = actor.GetMemory(Offsets.Main.Scale);
			this.scaleMem.Bind(this, nameof(this.Scale));

			if (this.isGpose)
			{
				this.cameraPositionMem = MemoryService.GetMarshaler(Offsets.Main.Gpose, Offsets.Main.Camera);

				if (this.LockCameraPosition)
				{
					this.cameraPositionMem.Value = this.CameraPosition;
				}
				else
				{
					this.cameraPositionMem.Value = actor.GetValue(Offsets.Main.Position);
				}

				this.cameraPositionMem.Bind(this, nameof(this.CameraPosition));
			}
		}
	}
}