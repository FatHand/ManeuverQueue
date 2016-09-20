using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using KSP.UI.Screens;
using KSP.IO;

using UnityEngine;
using UnityEngine.UI;

namespace FatHand
{
	[KSPAddon(KSPAddon.Startup.TrackingStation, false)]
	public class ManeuverQueue : MonoBehaviour
	{

		public enum FilterMode { 
			Undefined = -1, 
			Default, 
			Maneuver, 
			Name 
		};

		public static string[] filterModeLabels;
		public FilterMode currentMode
		{
			get
			{
				return _currentMode;
			}
			set
			{
				if (value != _currentMode)
				{
					_currentMode = value;
					this.SetVesselListForMode(_currentMode);

					// Unless the mode is undefined, persist the value (saved in onDestroy)
					if (_currentMode != FilterMode.Undefined)
					{
						this.pluginConfiguration.SetValue(ManeuverQueue.configurationModeKey, (int)_currentMode);
					}

				}
			}
		}


		protected SpaceTracking spaceTrackingScene;
		protected Rect windowPos;
		protected GUIStyle windowStyle;
		protected bool render;
		protected bool needsWidgetColorRender;
		protected const double minimumManeuverDeltaT = 15.0 * 60.0;
		protected Color nodePassedColor = new Color(231.0f / 255, 106.0f / 255, 106.0f / 255, 1);
		protected Color nodeWarningColor = new Color(254.0f / 255, 178.0f / 255, 0.0f / 255, 1);

		protected List<Vessel> defaultVessels
		{
			get
			{
				if (_defaultVessels == null)
				{
					_defaultVessels = GetTrackedVessels();
				}
				return _defaultVessels;
			}
			set
			{
				_defaultVessels = value;
			}
		}

		protected List<Vessel> vesselsSortedByNextManeuverNode
		{
			get
			{
				if (_vesselsSortedByNextManeuverNode == null) {
					_vesselsSortedByNextManeuverNode = this.VesselsSortedByNextManeuverNode();
				}
				return _vesselsSortedByNextManeuverNode;
			}

			set
			{
				_vesselsSortedByNextManeuverNode = value;
			}
		}

		protected List<Vessel> vesselsSortedByName
		{
			get
			{
				if (_vesselsSortedByName == null)
				{
					_vesselsSortedByName = this.VesselsSortedByName();
				}
				return _vesselsSortedByName;
			}

			set
			{
				_vesselsSortedByName = value;
			}
		}

		private static string configurationModeKey = "mode";

		private PluginConfiguration pluginConfiguration = PluginConfiguration.CreateForType<ManeuverQueue>();
		private Rect sideBarRect;
		private FilterMode _currentMode = FilterMode.Undefined;
		private List<Vessel> _defaultVessels;
		private List<Vessel> _vesselsSortedByNextManeuverNode;
		private List<Vessel> _vesselsSortedByName;



		// Lifecycle
		protected void Awake()
		{
		}

		protected void Start()
		{
			const float WINDOW_VERTICAL_POSITION = 36;

			this.spaceTrackingScene = (SpaceTracking)UnityEngine.Object.FindObjectOfType(typeof(SpaceTracking));

			this.sideBarRect = GetSideBarRect();

			this.windowPos = new Rect(this.sideBarRect.xMax, WINDOW_VERTICAL_POSITION, 10, 10);

			this.windowStyle = new GUIStyle(HighLogic.Skin.window)
			{
				margin = new RectOffset(),
				padding = new RectOffset(5, 5, 5, 5)
			};

			GameEvents.onGameSceneSwitchRequested.Add(this.onGameSceneSwitchRequested);
			GameEvents.onVesselDestroy.Add(this.onVesselDestroy);
			GameEvents.onVesselCreate.Add(this.onVesselCreate);
			GameEvents.onKnowledgeChanged.Add(this.onKnowledgeChanged);
			GameEvents.OnMapViewFiltersModified.Add(this.onMapViewFiltersModified);


			ManeuverQueue.filterModeLabels = Enum.GetValues(typeof(FilterMode)).Cast<FilterMode>().Where(
				x => x != FilterMode.Undefined).Select(
					x => ManeuverQueue.LabelForFilterMode(x)).ToArray();

			this.pluginConfiguration.load();
			this.currentMode = (FilterMode)this.pluginConfiguration.GetValue(ManeuverQueue.configurationModeKey, (int)FilterMode.Default);

			this.render = true;
		}

		protected void Update()
		{
		}

		protected void FixedUpdate()
		{
		}

		protected void OnDestroy()
		{
			GameEvents.onGameSceneSwitchRequested.Remove(this.onGameSceneSwitchRequested);

			GameEvents.onVesselDestroy.Remove(this.onVesselDestroy);
			GameEvents.onVesselCreate.Remove(this.onVesselCreate);
			GameEvents.onKnowledgeChanged.Remove(this.onKnowledgeChanged);
			GameEvents.OnMapViewFiltersModified.Remove(this.onMapViewFiltersModified);

			this.pluginConfiguration.save();

		}


		private void onGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes > fromToAction)
		{
			render = false;
		}

		protected void OnGUI()
		{
			if (this.render)
			{
				this.windowPos = GUILayout.Window(1, this.windowPos, this.ToolbarWindow, "", this.windowStyle, new GUILayoutOption[0]);

				if (this.currentMode == FilterMode.Maneuver)
				{

					// apply shading to vessel icons for maneuver nodes that have just moved into the past or soon state
					foreach (TrackingStationWidget widget in this.GetTrackingStationWidgets())
					{
						this.UpdateWidgetColorForCurrentTime(widget);
					}

					// reapply shading for close maneuver nodes if necessary
					if (this.needsWidgetColorRender)
					{
						this.RenderWidgetColors();
					}

				}

			}


		}

		protected void SetVesselListForMode(FilterMode mode)
		{
			switch (mode)
			{
				case FilterMode.Undefined:
					break;
				case FilterMode.Default:
					this.SetVesselList(this.defaultVessels);
					break;
				case FilterMode.Maneuver:
					this.SetVesselList(this.vesselsSortedByNextManeuverNode);
					break;
				case FilterMode.Name:
					this.SetVesselList(this.vesselsSortedByName);
					break;
				default:
					this.SetVesselList(this.defaultVessels);
					break;
			}
		}

		private List<Vessel> VesselsSortedByName()
		{

			var originalVessels = new List<Vessel>(this.defaultVessels);
			originalVessels.Sort((x, y) => x.vesselName.CompareTo(y.vesselName));

			return originalVessels;

		}

		private List<Vessel> VesselsSortedByNextManeuverNode()
		{
			var originalVessels = new List<Vessel>(this.defaultVessels);

			List<Vessel> filteredVessels = originalVessels.Where(vessel => (this.NextManeuverNodeForVessel(vessel) != null)).ToList();
			filteredVessels.Sort((x, y) => this.NextManeuverNodeForVessel(x).UT.CompareTo(this.NextManeuverNodeForVessel(y).UT));

			return filteredVessels;

		}

		protected void SetVesselList(List<Vessel> vessels)
		{
			if (this.spaceTrackingScene == null)
			{
				return;
			}

			this.spaceTrackingScene.GetType().GetField("trackedVessels", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this.spaceTrackingScene, vessels);

			MethodInfo clearMethod = this.spaceTrackingScene.GetType().GetMethod("ClearUIList", BindingFlags.NonPublic | BindingFlags.Instance);
			MethodInfo constructMethod = this.spaceTrackingScene.GetType().GetMethod("ConstructUIList", BindingFlags.NonPublic | BindingFlags.Instance);

			clearMethod.Invoke(this.spaceTrackingScene, new object[0]);
			constructMethod.Invoke(this.spaceTrackingScene, new object[0]);

			this.needsWidgetColorRender = true;

			this.ResetWidgetsForActiveVessel();
		}

		protected void RenderWidgetColors()
		{
			this.needsWidgetColorRender = false;

			// apply shading to vessel icons
			if (this.currentMode == FilterMode.Maneuver)
			{
				for (var i = 0; i < this.vesselsSortedByNextManeuverNode.Count() - 1; ++i)
				{
					Vessel vessel = this.vesselsSortedByNextManeuverNode.ElementAt(i);
					Vessel nextVessel = this.vesselsSortedByNextManeuverNode.ElementAt(i + 1);

					TrackingStationWidget vesselWidget = this.GetWidgetForVessel(vessel);

					double mnvTime1 = this.NextManeuverNodeForVessel(vessel).UT;
					double mnvTime2 = this.NextManeuverNodeForVessel(nextVessel).UT;

					// if two maneuver nodes are less than minimumManeuverDeltaT secs apart - yellow
					if (mnvTime2 - mnvTime1 < minimumManeuverDeltaT)
					{
						TrackingStationWidget nextVesselWidget = this.GetWidgetForVessel(nextVessel);


						if (vesselWidget != null)
						{
							this.ApplyColorToVesselWidget(vesselWidget, this.nodeWarningColor);
						}


						if (nextVesselWidget != null)
						{
							this.ApplyColorToVesselWidget(nextVesselWidget, this.nodeWarningColor);
						}
					}

					if (vesselWidget)
					{
						this.UpdateWidgetColorForCurrentTime(vesselWidget);
					}

				}
			}

		}

		protected string StatusStringForVessel(Vessel vessel)
		{
			ManeuverNode node = this.NextManeuverNodeForVessel(vessel);

			if (node != null)
			{

				return "dV - " + Convert.ToInt16(node.DeltaV.magnitude) + "m/s";
			}

			return "None";
		}

		protected ManeuverNode NextManeuverNodeForVessel(Vessel vessel)
		{
			if (vessel.flightPlanNode != null && vessel.flightPlanNode.HasNode("MANEUVER"))
			{
				ManeuverNode node = new ManeuverNode();
				node.Load(vessel.flightPlanNode.GetNode("MANEUVER"));
				return node;
			}

			return null;
		}

		protected void ApplyColorToVesselWidget(TrackingStationWidget widget, Color color)
		{
			var image = (Image)widget.iconSprite.GetType().GetField("image", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(widget.iconSprite);
			image.color = color;
		}

		protected void UpdateWidgetColorForCurrentTime(TrackingStationWidget widget)
		{

			double maneuverTime = this.NextManeuverNodeForVessel(widget.vessel).UT;

			// if the maneuver node is less than 15mins away - yellow
			if (maneuverTime < Planetarium.GetUniversalTime() + minimumManeuverDeltaT)
			{

				this.ApplyColorToVesselWidget(widget, this.nodeWarningColor);


			}

			// if the maneuver nodes is in the past - red
			if (maneuverTime < Planetarium.GetUniversalTime())
			{

				this.ApplyColorToVesselWidget(widget, this.nodePassedColor);


			}

		}

		protected List<Vessel> GetTrackedVessels()
		{
			if (this.spaceTrackingScene == null)
			{
				return null;
			}

			List<Vessel> originalVessels = (List<Vessel>)this.spaceTrackingScene.GetType().GetField("trackedVessels", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this.spaceTrackingScene);

			return new List<Vessel>(originalVessels);
		}

		protected List<TrackingStationWidget> GetTrackingStationWidgets()
		{
			return (List<TrackingStationWidget>)this.spaceTrackingScene.GetType().GetField("vesselWidgets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this.spaceTrackingScene);
		}

		protected TrackingStationWidget GetWidgetForVessel(Vessel vessel)
		{
			foreach (TrackingStationWidget widget in this.GetTrackingStationWidgets())
			{
				if (widget.vessel == vessel)
				{
					return widget;
				}
			}

			return null;
		}

		protected Vessel GetTrackingStationSelectedVessel()
		{
			return (Vessel)this.spaceTrackingScene.GetType().GetField("selectedVessel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this.spaceTrackingScene);
		}

		protected void SetTrackingStationSelectedVessel(Vessel vessel)
		{
			MethodInfo setVesselMethod = this.spaceTrackingScene.GetType().GetMethod("SetVessel", BindingFlags.NonPublic | BindingFlags.Instance);

			setVesselMethod.Invoke(this.spaceTrackingScene, new object[] { vessel, true });

		}

		protected void ResetWidgetsForActiveVessel()
		{
			Vessel selectedVessel = this.GetTrackingStationSelectedVessel();

			foreach (TrackingStationWidget widget in this.GetTrackingStationWidgets())
			{
				widget.toggle.isOn = widget.vessel == selectedVessel;

			}
		}

		private void ToolbarWindow(int windowID)
		{
			this.currentMode = (FilterMode)GUILayout.Toolbar((int)this.currentMode,
			                                                   ManeuverQueue.filterModeLabels,
															   HighLogic.Skin.button,
															   new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false) });


		}

		private void onVesselDestroy(Vessel vessel)
		{
			this.ClearCachedVesselLists();

		}

		private void onVesselCreate(Vessel vessel)
		{
			this.ClearCachedVesselLists();
		}

		private void onKnowledgeChanged(GameEvents.HostedFromToAction<IDiscoverable, DiscoveryLevels> data)
		{
			if ((data.to & DiscoveryLevels.Unowned) == DiscoveryLevels.Unowned && this.currentMode == FilterMode.Maneuver)
			{
				this.currentMode = FilterMode.Default;
			}

			this.ClearCachedVesselLists();
		}

		private void onMapViewFiltersModified(MapViewFiltering.VesselTypeFilter data)
		{
			this.needsWidgetColorRender = true;
		}

		private void ClearCachedVesselLists()
		{
			this.currentMode = FilterMode.Undefined;
			this.defaultVessels = null;
			this.vesselsSortedByName = null;
			this.vesselsSortedByNextManeuverNode = null;
		}

		public static string LabelForFilterMode(FilterMode mode)
		{
			switch (mode)
			{
				case FilterMode.Default:
					return "MET";
				case FilterMode.Maneuver:
					return "MNV";
				case FilterMode.Name:
					return "A-Z";
				default:
					return null;
			}


		}

		private static Rect GetSideBarRect()
		{
			GameObject sideBar = GameObject.Find("Side Bar");
			return ((RectTransform)sideBar.transform).rect;

		}


	}

}

