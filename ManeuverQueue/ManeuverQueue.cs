using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using KSP.UI.Screens;

using UnityEngine;

namespace FatHand
{
	[KSPAddon(KSPAddon.Startup.TrackingStation, false)]
	public class ManeuverQueue : MonoBehaviour
	{
		public enum FilterMode { Default, Maneuver, Name };

		public static string[] filterModeLabels;
		public FilterMode currentMode
		{
			get
			{
				return _currentMode;
			}
			set
			{
				if (value != _currentMode || currentVesselList == null)
				{
					_currentMode = value;
					this.SetVesselListForMode(_currentMode);
				}
			}
		}
					

		protected SpaceTracking spaceTrackingScene;
		protected Rect windowPos;
		protected GUIStyle windowStyle;
		protected bool render;

		private Rect sideBarRect;
		private FilterMode _currentMode;
		private List<Vessel> _defaultVessels;
		private List<Vessel> defaultVessels
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

		private List<Vessel> currentVesselList;


		// Lifecycle
		private void Awake()
		{
		}

		private void Start()
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

			ManeuverQueue.filterModeLabels = Enum.GetValues(typeof(FilterMode)).Cast<FilterMode>().Select(x => ManeuverQueue.LabelForFilterMode(x)).ToArray();

			this.currentMode = FilterMode.Default;

			this.render = true;
		}

		private void Update()
		{
		}

		private void FixedUpdate()
		{
		}

		private void OnDestroy()
		{
			GameEvents.onGameSceneSwitchRequested.Remove(this.onGameSceneSwitchRequested);

			GameEvents.onVesselDestroy.Remove(this.onVesselDestroy);
			GameEvents.onVesselCreate.Remove(this.onVesselCreate);

		}


		private void onGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes > fromToAction)
		{
			render = false;
		}

		private void OnGUI()
		{

			if (render)
			{

				this.windowPos = GUILayout.Window(1, this.windowPos, this.ToolbarWindow, "", this.windowStyle, new GUILayoutOption[0]);
			}


		}

		// Protected
		protected void SetVesselListForMode(FilterMode mode)
		{
			switch (mode)
			{
				case FilterMode.Default:
					this.SetVesselList(VesselsUnsorted());
					break;
				case FilterMode.Maneuver:
					this.SetVesselList(VesselsSortedByNextManeuverNode());
					break;
				case FilterMode.Name:
					this.SetVesselList(VesselsSortedByName());
					break;
				default:
					this.SetVesselList(VesselsUnsorted());
					break;
			}
		}

		protected List<Vessel> VesselsUnsorted()
		{
			return this.defaultVessels;
		}

		protected List<Vessel> VesselsSortedByName()
		{

			var originalVessels = new List<Vessel>(this.defaultVessels);
			originalVessels.Sort((x, y) => x.vesselName.CompareTo(y.vesselName));

			return originalVessels;

		}

		protected List<Vessel> VesselsSortedByNextManeuverNode()
		{
			var originalVessels = new List<Vessel>(this.defaultVessels);

			List<Vessel> filteredVessels = originalVessels.Where(vessel => (vessel.flightPlanNode != null && vessel.flightPlanNode.HasNode("MANEUVER"))).ToList();
			filteredVessels.Sort((x, y) => Convert.ToDouble(x.flightPlanNode.GetNode("MANEUVER").GetValue("UT")).CompareTo(Convert.ToDouble(y.flightPlanNode.GetNode("MANEUVER").GetValue("UT"))));

			return filteredVessels;

		}

		protected void SetVesselList(List<Vessel> vessels)
		{
			if (this.spaceTrackingScene == null)
			{
				return;
			}

			currentVesselList = vessels;

			this.spaceTrackingScene.GetType().GetField("trackedVessels", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this.spaceTrackingScene, vessels);

			MethodInfo clearMethod = this.spaceTrackingScene.GetType().GetMethod("ClearUIList", BindingFlags.NonPublic | BindingFlags.Instance);
			MethodInfo constructMethod = this.spaceTrackingScene.GetType().GetMethod("ConstructUIList", BindingFlags.NonPublic | BindingFlags.Instance);

			clearMethod.Invoke(this.spaceTrackingScene, new object[0]);
			constructMethod.Invoke(this.spaceTrackingScene, new object[0]);

		}

		protected List<Vessel> GetTrackedVessels()
		{
			if (this.spaceTrackingScene == null)
			{
				return null;
			}

			var originalVessels = (List<Vessel>)this.spaceTrackingScene.GetType().GetField("trackedVessels", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this.spaceTrackingScene);

			return new List<Vessel>(originalVessels);
		}

		// Private
		private void ToolbarWindow(int windowID)
		{
			this.currentMode = (FilterMode)GUILayout.Toolbar((int)this.currentMode,
			                                                   ManeuverQueue.filterModeLabels,
															   HighLogic.Skin.button,
															   new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false) });


		}

		private void onVesselDestroy(Vessel vessel)
		{
			this.defaultVessels = null;
			this.currentVesselList = null;
		}

		private void onVesselCreate(Vessel vessel)
		{
			this.defaultVessels = null;
			this.currentVesselList = null;
		}

		private void onVesselRename(Vessel vessel)
		{
			this.defaultVessels = null;
			this.currentVesselList = null;
		}

		// Public static

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

