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

				// rebuild the list if the value is changed or the list is uninitialized
				if (value != _currentMode || _defaultVessels == null)
				{

					// if we're switching from any mode other than maneuver mode, save the filter state
					if (_currentMode != FilterMode.Maneuver && _currentMode != FilterMode.Undefined)
					{
						ManeuverQueue.savedFilterState = MapViewFiltering.vesselTypeFilter;

					}

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
		protected bool needsRerender;
		protected bool needsWidgetColorRender;
		protected const double minimumManeuverDeltaT = 15.0 * 60.0;
		protected Color nodePassedColor = new Color(255.0f / 255, 58.0f / 255, 58.0f / 255, 1);
		protected Color nodeWarningColor = new Color(255.0f / 255, 255.0f / 255, 58.0f / 255, 1);

		protected List<Vessel> defaultVessels
		{
			get
			{
				if (_defaultVessels == null)
				{
					_defaultVessels = this.GetTrackedVessels();
				}
				return _defaultVessels;
			}
			set
			{
				_defaultVessels = value;
				_vesselsSortedByName = null;
				_vesselsSortedByNextManeuverNode = null;
				_guardedVessels = null;

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

		// vessels with maneuver nodes in the future
		// 'guarded' - prevent warping beyond next node
		protected List<Vessel> guardedVessels
		{
			get
			{
				if (_guardedVessels == null)
				{
					_guardedVessels = this.vesselsSortedByNextManeuverNode.Where((Vessel arg) => 
						{ return this.NextManeuverNodeForVessel(arg).UT - Planetarium.GetUniversalTime() > ManeuverQueue.minimumManeuverDeltaT; }).ToList();
					                                                             
				}

				return _guardedVessels;
			}

			set
			{
				_guardedVessels = value;
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
		private static string configurationFiltersKey = "filters";


		private PluginConfiguration pluginConfiguration = PluginConfiguration.CreateForType<ManeuverQueue>();
		private Rect sideBarRect;
		private FilterMode _currentMode = FilterMode.Undefined;
		private List<Vessel> _defaultVessels;
		private List<Vessel> _vesselsSortedByNextManeuverNode;
		private List<Vessel> _vesselsSortedByName;
		private List<Vessel> _guardedVessels;
		private static MapViewFiltering.VesselTypeFilter savedFilterState;



		// Lifecycle
		protected void Awake()
		{
		}

		protected void Start()
		{
			const float WINDOW_VERTICAL_POSITION = 36;

			this.pluginConfiguration.load();

			if (MapViewFiltering.vesselTypeFilter != MapViewFiltering.VesselTypeFilter.All)
			{
				ManeuverQueue.savedFilterState = MapViewFiltering.vesselTypeFilter;
			}
			else {
				ManeuverQueue.savedFilterState = (MapViewFiltering.VesselTypeFilter)this.pluginConfiguration.GetValue(ManeuverQueue.configurationFiltersKey, (int)MapViewFiltering.VesselTypeFilter.All);
			}

			this.pluginConfiguration.SetValue(ManeuverQueue.configurationFiltersKey, (int)MapViewFiltering.VesselTypeFilter.All);
			this.pluginConfiguration.save();

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

			this.currentMode = (FilterMode)this.pluginConfiguration.GetValue(ManeuverQueue.configurationModeKey, (int)FilterMode.Default);

			this.render = true;

		}

		protected void Update()
		{
			if (this.guardedVessels.Count() > 0 && this.NextManeuverNodeForVessel(this.guardedVessels.ElementAt(0)).UT - Planetarium.GetUniversalTime() <= ManeuverQueue.minimumManeuverDeltaT)
			{
				TimeWarp.SetRate(0, true, true);
				this.guardedVessels = null;
			}
		}

		protected void FixedUpdate()
		{
		}

		private void onMapEntered()
		{

			this.pluginConfiguration.load();

			MapViewFiltering.VesselTypeFilter stateToRestore = (MapViewFiltering.VesselTypeFilter)this.pluginConfiguration.GetValue(ManeuverQueue.configurationFiltersKey, (int)MapViewFiltering.VesselTypeFilter.All);
			if (stateToRestore != MapViewFiltering.VesselTypeFilter.All)
			{
				MapViewFiltering.SetFilter(stateToRestore);
				this.pluginConfiguration.SetValue(ManeuverQueue.configurationFiltersKey, ManeuverQueue.savedFilterState);
				this.pluginConfiguration.save();

			}

			GameEvents.OnMapEntered.Remove(this.onMapEntered);

		}

		private void onGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> data)
		{
			this.render = false;
		}

		protected void OnDestroy()
		{
			GameEvents.onGameSceneSwitchRequested.Remove(this.onGameSceneSwitchRequested);

			GameEvents.onVesselDestroy.Remove(this.onVesselDestroy);
			GameEvents.onVesselCreate.Remove(this.onVesselCreate);
			GameEvents.onKnowledgeChanged.Remove(this.onKnowledgeChanged);
			GameEvents.OnMapViewFiltersModified.Remove(this.onMapViewFiltersModified);

			// This is a hack to ensure filter settings are retained
			// Necessary because there doesn't seem to be any reliable way to restore filters when leaving the tracking station
			this.pluginConfiguration.SetValue(ManeuverQueue.configurationFiltersKey, (int)ManeuverQueue.savedFilterState);
			GameEvents.OnMapEntered.Add(this.onMapEntered);

			this.pluginConfiguration.save();

		}

		protected void OnGUI()
		{
			if (this.render)
			{
				this.windowPos = GUILayout.Window(1, this.windowPos, this.ToolbarWindow, "", this.windowStyle, new GUILayoutOption[0]);

				if (this.needsRerender)
				{
					this.SetVesselListForMode(this.currentMode);
					this.needsRerender = false;
				}

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
					this.SetVesselList(this.defaultVessels, ManeuverQueue.savedFilterState);
					break;
				case FilterMode.Maneuver:
					this.SetVesselList(this.vesselsSortedByNextManeuverNode, MapViewFiltering.VesselTypeFilter.All);
					break;
				case FilterMode.Name:
					this.SetVesselList(this.vesselsSortedByName, ManeuverQueue.savedFilterState);
					break;
				default:
					this.SetVesselList(this.defaultVessels, ManeuverQueue.savedFilterState);
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

		protected void SetVesselList(List<Vessel> vessels, MapViewFiltering.VesselTypeFilter filters)
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

			MapViewFiltering.SetFilter(filters);

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
					if (mnvTime2 - mnvTime1 < ManeuverQueue.minimumManeuverDeltaT)
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
			ManeuverNode node = this.NextManeuverNodeForVessel(widget.vessel);

			if (node == null)
			{
				return;
			}

			double maneuverTime = node.UT;

			// if the maneuver node is less than 15mins away - yellow
			if (maneuverTime < Planetarium.GetUniversalTime() + ManeuverQueue.minimumManeuverDeltaT)
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

			this.ResetVesselList();
			this.needsRerender = true;

		}

		private void onVesselCreate(Vessel vessel)
		{
			this.ResetVesselList();
		}

		private void onKnowledgeChanged(GameEvents.HostedFromToAction<IDiscoverable, DiscoveryLevels> data)
		{

			if ((data.to & DiscoveryLevels.Unowned) == DiscoveryLevels.Unowned && this.currentMode == FilterMode.Maneuver)
			{
				this.currentMode = FilterMode.Default;

			}

			this.ResetVesselList();
			this.needsRerender = true;


		}

		private void onMapViewFiltersModified(MapViewFiltering.VesselTypeFilter data)
		{

			this.needsWidgetColorRender = true;
		}

		private void ResetVesselList()
		{

			this.defaultVessels = null;
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

