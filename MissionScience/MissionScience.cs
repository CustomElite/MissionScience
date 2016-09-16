using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using KSP.UI.Screens;
using KSP.IO;

namespace MissionScience
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]

    public class MissionScience : MonoBehaviour
    {
        public static MissionScience Instance;
        private bool modIsActive;
        private ApplicationLauncherButton appLauncherButton;

        /* GUI Variables */
        private int locWindowId;
        private Rect locWindowRect;
        private bool locWindowVisible;

        private int expWindowId;
        private Rect expWindowRect;
        private bool expWindowVisible;

        private int optWindowId;
        private Rect optWindowRect;
        private bool optWindowVisible;

        /* Settings Variables */
        private bool getFullScience;
        private bool autoExperiment;
        private int minExperimentValue;

        /* Class Private Variables */
        private string currBiome;
        private Vessel currVessel;
        private CelestialBody currBody;
        private List<ModuleScienceExperiment> availableExperiments;
        private List<ModuleScienceExperiment> onVesselExperiments;
        private List<ModuleScienceExperiment> completeExperiments;
        private ExperimentSituations experimentSituation;

        private void setupAppButton()
        {
            if (ApplicationLauncher.Ready)
            {
                if (modIsActive)
                {
                    if (appLauncherButton == null)
                    {
                        appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
                            toggleExpWindow,
                            toggleExpWindow,
                            null,
                            null,
                            null,
                            null,
                            ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                            GameDatabase.Instance.GetTexture("MissionScience/Textures/btnScience", false));
                    }
                } else {
                    if (appLauncherButton != null)
                    {
                        ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                        expWindowVisible = false;
                        locWindowVisible = false;
                        optWindowVisible = false;
                    }
                }
            }
        }

        private void toggleExpWindow()
        {
            expWindowVisible = !expWindowVisible;
        }

        private void renderExpWindow(int id)
        {
            if (expWindowVisible)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Location Info"))
                {
                    locWindowVisible = !locWindowVisible;
                }
                if (GUILayout.Button("Options"))
                {
                    optWindowVisible = !optWindowVisible;
                }
                GUILayout.BeginVertical();
                if (GUILayout.Button("Close Window"))
                {
                    expWindowVisible = false;
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Space(5);
                if (onVesselExperiments.Count > 0)
                {
                    foreach (ModuleScienceExperiment exp in onVesselExperiments)
                    {
                        if (GUILayout.Button(exp.experimentActionName))
                        {
                            exp.DeployExperiment();
                        }
                    }
                } else {
                    GUILayout.Label("No more active experiments on vessel.");
                }
                GUILayout.Space(4);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUI.DragWindow();
            }
        }

        private void renderLocWindow(int id)
        {
            if (locWindowVisible)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                if (GUILayout.Button("Close"))
                {
                    locWindowVisible = false;
                }
                GUILayout.Label("Biome: " + currBiome);
                GUILayout.Label("Situation: " + experimentSituation);
                GUILayout.Label("Body: " + currBody);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUI.DragWindow();
            }
        }

        private string getBiome()
        {
            if (currVessel != null)
            {
                if (currVessel.mainBody.BiomeMap != null)
                {
                    if (!string.IsNullOrEmpty(currVessel.landedAt))
                    {
                        return Vessel.GetLandedAtString(currVessel.landedAt);
                    } else {
                        return ScienceUtil.GetExperimentBiome(currVessel.mainBody, currVessel.latitude, currVessel.longitude);
                    }
                }
            }

            return string.Empty;
        }

        private List<ModuleScienceExperiment> getExperiments()
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceExperiment>();
        }

        private bool checkSurfaceSample()
        {
            if (GameVariables.Instance.GetScienceCostLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment)) >= 500)
            {
                if (currBody.bodyName == "Kerbin")
                {
                    return true;
                } else {
                    if (GameVariables.Instance.UnlockedEVA(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool checkExperiment(ModuleScienceExperiment exp)
        {
            /* Add Feature: Check for minimum science value
             * && ResearchAndDevelopment.GetScienceValue(exp.experiment.baseValue * exp.experiment.dataScale, getExperimentSubject(exp.experiment)) > minScienceValue; 
            */
            bool rtn = !exp.Inoperable && !exp.Deployed && exp.experiment.IsAvailableWhile(experimentSituation, currBody);

            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER && exp.experiment.id == "surfaceSample")
            {
                rtn = rtn && checkSurfaceSample();
            }

            return rtn;
        }

        private bool checkStatus()
        {
            return FlightGlobals.ActiveVessel.loaded &&
                (currVessel != FlightGlobals.ActiveVessel || currBiome != getBiome() || experimentSituation != ScienceUtil.GetExperimentSituation(currVessel) || currBody != currVessel.mainBody);
        }

        private void updateStatus()
        {
            currVessel = FlightGlobals.ActiveVessel;
            currBiome = getBiome();
            currBody = currVessel.mainBody;
            experimentSituation = ScienceUtil.GetExperimentSituation(currVessel);
            availableExperiments = getExperiments();
            onVesselExperiments = new List<ModuleScienceExperiment>();
            completeExperiments = new List<ModuleScienceExperiment>();

            if (availableExperiments.Count() > 0)
            {
                foreach (ModuleScienceExperiment exp in availableExperiments)
                {
                    if (exp.GetData().Length > 0)
                    {
                        completeExperiments.Add(exp);
                    } else if (checkExperiment(exp)) {
                        onVesselExperiments.Add(exp);
                    }
                }
            }
            
        }

        public void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            modIsActive = false;

            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX)
            {
                modIsActive = true;
                locWindowRect = new Rect();
                locWindowVisible = false;
                expWindowRect = new Rect();
                expWindowVisible = false;
            }
        }

        public void Start()
        {
            if (modIsActive)
            {
                locWindowId = GUIUtility.GetControlID(FocusType.Passive);
                locWindowRect.width = 250;
                locWindowRect.x = (Screen.width - 250) / 2;
                locWindowRect.y = Screen.height / 5;

                optWindowId = GUIUtility.GetControlID(FocusType.Passive);
                optWindowRect.width = 250;
                optWindowRect.x = (Screen.width - 250) / 2;
                optWindowRect.y = Screen.height / 5;

                expWindowId = GUIUtility.GetControlID(FocusType.Passive);
                expWindowRect.width = 400;
                expWindowRect.x = (Screen.width - 400) / 2;
                expWindowRect.y = Screen.height / 5;

                GameEvents.onGUIApplicationLauncherReady.Add(setupAppButton);

            }
        }

        public void OnDestroy()
        {
            if (modIsActive)
            {
                GameEvents.onGUIApplicationLauncherReady.Remove(setupAppButton);
            }
        }

        public void OnGUI()
        {
            if (!modIsActive) return;

            if (expWindowVisible)
            {
                GUI.skin = UnityEngine.GUI.skin;
                expWindowRect = GUILayout.Window(
                    expWindowId,
                    expWindowRect,
                    renderExpWindow,
                    "Mission: Science",
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
            }

            if (locWindowVisible)
            {
                GUI.skin = UnityEngine.GUI.skin;
                locWindowRect = GUILayout.Window(
                    locWindowId,
                    locWindowRect,
                    renderLocWindow,
                    "Location / Situation Information",
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
            }
        }

        public void FixedUpdate()
        {
            if (checkStatus())
            {
                updateStatus();
            }
        }
    }
}
