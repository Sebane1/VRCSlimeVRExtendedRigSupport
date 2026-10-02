using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

public class EarRigInjector : EditorWindow
{
    private AnimatorController targetController;
    private VRCExpressionParameters selectedExpParams;

    private Transform leftEarBone;
    private Transform rightEarBone;

    private string clipOutputFolder = "Assets/Animations/EarTracking";

    private float maxPitch = -90f;
    private float maxYaw = -90f;
    private float maxVertical = 0.01f;
    private int yawSteps = 15;
    private bool useOSCSmoothPath;

    private Vector2 scroll;

    private const string LeftPitch = "LeftEarPitch";
    private const string LeftYaw = "LeftEarYaw";
    private const string RightPitch = "RightEarPitch";
    private const string RightYaw = "RightEarYaw";
    private const string EarVertical = "EarVertical";

    private const int EncodedLevels = 15;
    private const int EncodedMiddle = 7;
    private const int NeutralGrayCode = 4;

    private const string PrefPrefix = "VRCSlimeVRExtendedRigSupport_Ear_";

    [MenuItem("Tools/Ear Rig/Add Ear Tracking Compatibility")]
    public static void Open()
    {
        GetWindow<EarRigInjector>("Ear Tracking Configurator");
    }

    private void OnEnable()
    {
        useOSCSmoothPath = EditorPrefs.GetBool(PrefPrefix + "UseOSCSmooth", false);

        clipOutputFolder = EditorPrefs.GetString(
            PrefPrefix + "ClipOutputFolder",
            "Assets/Animations/EarTracking"
        );

        targetController = LoadAssetPreference<AnimatorController>(
            PrefPrefix + "TargetController"
        );

        selectedExpParams = LoadAssetPreference<VRCExpressionParameters>(
            PrefPrefix + "ExpressionParameters"
        );

        leftEarBone = LoadTransformPreference(PrefPrefix + "LeftEarBone");
        rightEarBone = LoadTransformPreference(PrefPrefix + "RightEarBone");
    }

    private void OnDisable()
    {
        EditorPrefs.SetBool(PrefPrefix + "UseOSCSmooth", useOSCSmoothPath);
        EditorPrefs.SetString(PrefPrefix + "ClipOutputFolder", clipOutputFolder);

        SaveAssetPreference(
            PrefPrefix + "TargetController",
            targetController
        );

        SaveAssetPreference(
            PrefPrefix + "ExpressionParameters",
            selectedExpParams
        );

        SaveTransformPreference(
            PrefPrefix + "LeftEarBone",
            leftEarBone
        );

        SaveTransformPreference(
            PrefPrefix + "RightEarBone",
            rightEarBone
        );
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "SlimeVR Ear Tracking Configurator",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space();

        selectedExpParams =
            (VRCExpressionParameters)EditorGUILayout.ObjectField(
                "VRC Expression Parameters",
                selectedExpParams,
                typeof(VRCExpressionParameters),
                false
            );

        targetController =
            (AnimatorController)EditorGUILayout.ObjectField(
                "FX Animator Controller",
                targetController,
                typeof(AnimatorController),
                false
            );

        clipOutputFolder =
            EditorGUILayout.TextField(
                "Clip Output Folder",
                clipOutputFolder
            );

        EditorGUILayout.Space();

        useOSCSmoothPath =
            EditorGUILayout.Toggle(
                "Uses OSC Smooth",
                useOSCSmoothPath
            );

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Ear Bones",
            EditorStyles.boldLabel
        );

        leftEarBone =
            (Transform)EditorGUILayout.ObjectField(
                "Left Ear",
                leftEarBone,
                typeof(Transform),
                true
            );

        rightEarBone =
            (Transform)EditorGUILayout.ObjectField(
                "Right Ear",
                rightEarBone,
                typeof(Transform),
                true
            );

        if (GUILayout.Button("Auto Fill Bones"))
        {
            AutoFillEarBones();
        }

        EditorGUILayout.Space();

        if ((yawSteps & 1) == 0)
        {
            yawSteps++;
        }

        EditorGUILayout.Space();

        GUI.enabled =
            targetController != null &&
            selectedExpParams != null &&
            (leftEarBone != null || rightEarBone != null);

        if (GUILayout.Button("Generate Ear Support"))
        {
            Generate();
        }

        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    private void Generate()
    {
        if (targetController == null)
        {
            EditorUtility.DisplayDialog(
                "Missing FX Controller",
                "Select an FX Animator Controller first.",
                "OK"
            );
            return;
        }

        if (selectedExpParams == null)
        {
            EditorUtility.DisplayDialog(
                "Missing Expression Parameters",
                "Select a VRC Expression Parameters asset first.",
                "OK"
            );
            return;
        }

        if (leftEarBone == null && rightEarBone == null)
        {
            EditorUtility.DisplayDialog(
                "Missing Ear Bones",
                "Assign at least one ear bone.",
                "OK"
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(clipOutputFolder))
        {
            clipOutputFolder = "Assets/Animations/EarTracking";
        }

        clipOutputFolder =
            clipOutputFolder
                .Replace("\\", "/")
                .TrimEnd('/');

        if (!clipOutputFolder.StartsWith("Assets"))
        {
            EditorUtility.DisplayDialog(
                "Invalid Clip Folder",
                "The clip output folder must be inside Assets.",
                "OK"
            );
            return;
        }

        yawSteps = Mathf.Clamp(yawSteps, 3, 31);
        maxVertical = Mathf.Max(0f, maxVertical);

        if ((yawSteps & 1) == 0)
        {
            yawSteps++;
        }

        EnsureFolder(clipOutputFolder);
        EnsureAnimatorBoolParameter("IsLocal");
        if (leftEarBone != null)
        {
            GenerateEar(
                "LeftEar",
                leftEarBone,
                LeftPitch,
                LeftYaw
            );
        }

        if (rightEarBone != null)
        {
            GenerateEar(
                "RightEar",
                rightEarBone,
                RightPitch,
                RightYaw
            );
        }

        GenerateSharedVertical();

        EditorUtility.SetDirty(selectedExpParams);
        EditorUtility.SetDirty(targetController);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Ear configuration completed!",
            "Ear tracking support created successfully!",
            "OK"
        );
    }

    private void GenerateEar(
        string sideName,
        Transform bone,
        string pitchParameter,
        string yawParameter)
    {
        AddUnsyncedFloatParameter(pitchParameter);
        AddUnsyncedFloatParameter(yawParameter);

        AddEncodedBoolParameters(pitchParameter);
        AddEncodedBoolParameters(yawParameter);

        RemoveExistingLayer(sideName);
        RemoveExistingLayer(sideName + "_Pitch");
        RemoveExistingLayer(sideName + "_Yaw");
        RemoveExistingLayer(sideName + "_Pitch4BitEncode");
        RemoveExistingLayer(sideName + "_Yaw4BitEncode");
        RemoveExistingLayer(sideName + "_Pitch4BitDecode");
        RemoveExistingLayer(sideName + "_Yaw4BitDecode");

        string controllerPath =
            AssetDatabase.GetAssetPath(targetController);

        if (string.IsNullOrEmpty(controllerPath))
        {
            Debug.LogError(
                "[EarRig] The selected AnimatorController is not a saved asset."
            );
            return;
        }

        GenerateFourBitEncoderLayer(
            sideName + "_Pitch4BitEncode",
            pitchParameter,
            controllerPath
        );

        GenerateFourBitEncoderLayer(
            sideName + "_Yaw4BitEncode",
            yawParameter,
            controllerPath
        );

        GenerateFourBitDecoderLayer(
            sideName + "_Pitch4BitDecode",
            pitchParameter,
            controllerPath
        );

        GenerateFourBitDecoderLayer(
            sideName + "_Yaw4BitDecode",
            yawParameter,
            controllerPath
        );

        Debug.Log(
            $"[EarRig] {sideName}: RAW OSC Float -> ParameterDriver 4-bit synced Bool encoder; " +
            "remote bits -> RAW Float decoder -> OSCmooth proxy (when enabled) -> motion."
        );

        string resolvedPitchParameter =
            useOSCSmoothPath
                ? "OSCm/Proxy/" + pitchParameter
                : pitchParameter;

        string resolvedYawParameter =
            useOSCSmoothPath
                ? "OSCm/Proxy/" + yawParameter
                : yawParameter;

        EnsureAnimatorFloatParameter(resolvedPitchParameter);
        EnsureAnimatorFloatParameter(resolvedYawParameter);

        var stateMachine =
            new AnimatorStateMachine
            {
                name = sideName
            };

        AssetDatabase.AddObjectToAsset(
            stateMachine,
            controllerPath
        );
        var yawTree =
            new BlendTree
            {
                name = sideName + "_YawPitch_BlendTree",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false,
                blendParameter = resolvedYawParameter
            };

        AssetDatabase.AddObjectToAsset(
            yawTree,
            controllerPath
        );

        for (int i = 0; i < yawSteps; i++)
        {
            float normalizedYaw =
                Mathf.Lerp(
                    -1f,
                    1f,
                    i / (float)(yawSteps - 1)
                );

            float yawDegrees =
                normalizedYaw * maxYaw;

            string yawLabel =
                MakeYawLabel(
                    i,
                    yawSteps,
                    normalizedYaw
                );

            var pitchTree =
                new BlendTree
                {
                    name =
                        sideName +
                        "_" +
                        yawLabel +
                        "_PitchTree",

                    blendType = BlendTreeType.Simple1D,
                    useAutomaticThresholds = false,
                    blendParameter = resolvedPitchParameter
                };

            AssetDatabase.AddObjectToAsset(
                pitchTree,
                controllerPath
            );

            AnimationClip pitchNegative =
                CreateWorldRelativeRotationClip(
                    sideName + "_" + yawLabel + "_PitchNegative",
                    bone,
                    -maxPitch,
                    yawDegrees
                );

            AnimationClip pitchNeutral =
                CreateWorldRelativeRotationClip(
                    sideName + "_" + yawLabel + "_PitchNeutral",
                    bone,
                    0f,
                    yawDegrees
                );

            AnimationClip pitchPositive =
                CreateWorldRelativeRotationClip(
                    sideName + "_" + yawLabel + "_PitchPositive",
                    bone,
                    maxPitch,
                    yawDegrees
                );

            pitchTree.AddChild(
                pitchNegative,
                -1f
            );

            pitchTree.AddChild(
                pitchNeutral,
                0f
            );

            pitchTree.AddChild(
                pitchPositive,
                1f
            );

            EditorUtility.SetDirty(pitchTree);

            yawTree.AddChild(
                pitchTree,
                normalizedYaw
            );
        }

        AnimatorState state =
            stateMachine.AddState("Tracking");

        state.motion = yawTree;

        state.writeDefaultValues = true;

        stateMachine.defaultState = state;

        var layer =
            new AnimatorControllerLayer
            {
                name = sideName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

        var layers =
            targetController.layers.ToList();

        layers.Add(layer);

        targetController.layers =
            layers.ToArray();

        EditorUtility.SetDirty(yawTree);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(targetController);
    }

    private void GenerateSharedVertical()
    {
        AddUnsyncedFloatParameter(EarVertical);

        AddEncodedBoolParameters(EarVertical);

        RemoveExistingLayer("EarVertical");
        RemoveExistingLayer("EarVertical_4BitEncode");
        RemoveExistingLayer("EarVertical_4BitDecode");

        string controllerPath =
            AssetDatabase.GetAssetPath(targetController);

        if (string.IsNullOrEmpty(controllerPath))
        {
            Debug.LogError(
                "[EarRig] The selected AnimatorController is not a saved asset."
            );
            return;
        }

        GenerateFourBitEncoderLayer(
            "EarVertical_4BitEncode",
            EarVertical,
            controllerPath
        );

        GenerateFourBitDecoderLayer(
            "EarVertical_4BitDecode",
            EarVertical,
            controllerPath
        );

        string resolvedVerticalParameter =
            useOSCSmoothPath
                ? "OSCm/Proxy/" + EarVertical
                : EarVertical;

        EnsureAnimatorFloatParameter(resolvedVerticalParameter);

        var stateMachine =
            new AnimatorStateMachine
            {
                name = "EarVertical"
            };

        AssetDatabase.AddObjectToAsset(
            stateMachine,
            controllerPath
        );

        var verticalTree =
            new BlendTree
            {
                name = "EarVertical_BlendTree",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false,
                blendParameter = resolvedVerticalParameter
            };

        AssetDatabase.AddObjectToAsset(
            verticalTree,
            controllerPath
        );

        List<Transform> activeBones = new List<Transform>();
        if (leftEarBone != null) activeBones.Add(leftEarBone);
        if (rightEarBone != null) activeBones.Add(rightEarBone);

        AnimationClip verticalDown =
            CreateVerticalClip(
                "EarVertical_Down",
                activeBones,
                -maxVertical
            );

        AnimationClip verticalNeutral =
            CreateVerticalClip(
                "EarVertical_Neutral",
                activeBones,
                0f
            );

        AnimationClip verticalUp =
            CreateVerticalClip(
                "EarVertical_Up",
                activeBones,
                maxVertical
            );

        verticalTree.AddChild(verticalDown, -1f);
        verticalTree.AddChild(verticalNeutral, 0f);
        verticalTree.AddChild(verticalUp, 1f);

        EditorUtility.SetDirty(verticalTree);

        AnimatorState state = stateMachine.AddState("Tracking");
        state.motion = verticalTree;
        state.writeDefaultValues = true;
        stateMachine.defaultState = state;

        var layer =
            new AnimatorControllerLayer
            {
                name = "EarVertical",
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

        var layers = targetController.layers.ToList();
        layers.Add(layer);
        targetController.layers = layers.ToArray();

        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(targetController);
    }

    private void GenerateFourBitEncoderLayer(
        string layerName,
        string sourceFloatParam,
        string controllerPath)
    {
        var stateMachine =
            new AnimatorStateMachine
            {
                name = layerName
            };

        AssetDatabase.AddObjectToAsset(
            stateMachine,
            controllerPath
        );

        AnimatorState defaultState =
            stateMachine.AddState("Idle");

        stateMachine.defaultState = defaultState;

        for (int level = 0; level < EncodedLevels; level++)
        {
            AnimatorState stepState =
                stateMachine.AddState("SetLevel_" + level);

            stepState.writeDefaultValues = true;

            var driver =
                stepState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

            driver.localOnly = true;

            int gray =
                LevelToGrayCode(level);

            for (int b = 0; b < 4; b++)
            {
                bool bitValue =
                    ((gray >> b) & 1) != 0;

                driver.parameters.Add(
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        name =
                            GetBitParamName(
                                sourceFloatParam,
                                b
                            ),

                        value =
                            bitValue ? 1f : 0f
                    }
                );
            }

            float threshold =
                GetLevelThreshold(level);

            AnimatorStateTransition trans =
                defaultState.AddTransition(stepState);

            trans.hasExitTime = false;
            trans.exitTime = 0f;
            trans.duration = 0f;
            trans.canTransitionToSelf = false;

            trans.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "IsLocal"
            );

            if (level == 0)
            {
                trans.AddCondition(
                    AnimatorConditionMode.Less,
                    threshold,
                    sourceFloatParam
                );
            }
            else
            {
                float prevThreshold =
                    GetLevelThreshold(level - 1);

                trans.AddCondition(
                    AnimatorConditionMode.Greater,
                    prevThreshold,
                    sourceFloatParam
                );

                if (level < EncodedLevels - 1)
                {
                    trans.AddCondition(
                        AnimatorConditionMode.Less,
                        threshold,
                        sourceFloatParam
                    );
                }
            }

            AnimatorStateTransition returnTrans =
                stepState.AddTransition(defaultState);

            returnTrans.hasExitTime = false;
            returnTrans.exitTime = 0f;
            returnTrans.duration = 0f;
            returnTrans.canTransitionToSelf = false;

            returnTrans.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "IsLocal"
            );

            if (level == 0)
            {
                returnTrans.AddCondition(
                    AnimatorConditionMode.Greater,
                    threshold,
                    sourceFloatParam
                );
            }
            else
            {
                float prevThreshold =
                    GetLevelThreshold(level - 1);

                returnTrans.AddCondition(
                    AnimatorConditionMode.Less,
                    prevThreshold,
                    sourceFloatParam
                );

                if (level < EncodedLevels - 1)
                {
                    returnTrans.AddCondition(
                        AnimatorConditionMode.Greater,
                        threshold,
                        sourceFloatParam
                    );
                }
            }
        }

        var layer =
            new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

        var layers =
            targetController.layers.ToList();

        layers.Add(layer);

        targetController.layers =
            layers.ToArray();

        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(targetController);
    }

    private void GenerateFourBitDecoderLayer(
        string layerName,
        string sourceFloatParam,
        string controllerPath)
    {
        var stateMachine =
            new AnimatorStateMachine
            {
                name = layerName
            };

        AssetDatabase.AddObjectToAsset(
            stateMachine,
            controllerPath
        );

        AnimatorState defaultState =
            stateMachine.AddState("Idle");

        stateMachine.defaultState = defaultState;

        for (int level = 0; level < EncodedLevels; level++)
        {
            AnimatorState stepState =
                stateMachine.AddState("DecodeLevel_" + level);

            stepState.writeDefaultValues = true;

            var driver =
                stepState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

            driver.localOnly = false;

            float decodedFloatValue =
                LevelToDecodedFloat(level);

            driver.parameters.Add(
                new VRC_AvatarParameterDriver.Parameter
                {
                    name = sourceFloatParam,
                    value = decodedFloatValue
                }
            );

            AnimatorStateTransition trans =
                defaultState.AddTransition(stepState);

            trans.hasExitTime = false;
            trans.exitTime = 0f;
            trans.duration = 0f;
            trans.canTransitionToSelf = false;

            trans.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "IsLocal"
            );

            int gray =
                LevelToGrayCode(level);

            for (int b = 0; b < 4; b++)
            {
                bool bitValue =
                    ((gray >> b) & 1) != 0;

                trans.AddCondition(
                    bitValue
                        ? AnimatorConditionMode.If
                        : AnimatorConditionMode.IfNot,
                    0f,
                    GetBitParamName(
                        sourceFloatParam,
                        b
                    )
                );
            }

            AnimatorStateTransition returnTrans =
                stepState.AddTransition(defaultState);

            returnTrans.hasExitTime = false;
            returnTrans.exitTime = 0f;
            returnTrans.duration = 0f;
            returnTrans.canTransitionToSelf = false;

            returnTrans.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "IsLocal"
            );

            for (int b = 0; b < 4; b++)
            {
                bool bitValue =
                    ((gray >> b) & 1) != 0;

                returnTrans.AddCondition(
                    bitValue
                        ? AnimatorConditionMode.IfNot
                        : AnimatorConditionMode.If,
                    0f,
                    GetBitParamName(
                        sourceFloatParam,
                        b
                    )
                );
            }
        }

        var layer =
            new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

        var layers =
            targetController.layers.ToList();

        layers.Add(layer);

        targetController.layers =
            layers.ToArray();

        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(targetController);
    }

    private static int LevelToGrayCode(int level)
    {
        int relative = level - EncodedMiddle;
        int magnitude = Mathf.Abs(relative);
        int grayMag = magnitude ^ (magnitude >> 1);
        int signBit = relative < 0 ? 1 : 0;
        return (grayMag << 1) | signBit;
    }

    private static float GetLevelThreshold(int level)
    {
        float stepSize = 2f / EncodedLevels;
        return -1f + (level + 1) * stepSize;
    }

    private static float LevelToDecodedFloat(int level)
    {
        float stepSize = 2f / EncodedLevels;
        return -1f + (level + 0.5f) * stepSize;
    }

    private static string GetBitParamName(string baseParam, int bitIndex)
    {
        return baseParam + "_Bit" + bitIndex;
    }

    private void AddEncodedBoolParameters(string baseParamName)
    {
        for (int i = 0; i < 4; i++)
        {
            string bitName = GetBitParamName(baseParamName, i);
            bool bitDefault = ((NeutralGrayCode >> i) & 1) != 0;

            AddSyncedBoolParameter(bitName, bitDefault);
            EnsureAnimatorBoolParameter(bitName);
        }
    }

    private void AddUnsyncedFloatParameter(string paramName)
    {
        EnsureAnimatorFloatParameter(paramName);

        if (selectedExpParams == null)
        {
            return;
        }

        List<VRCExpressionParameters.Parameter> list =
            selectedExpParams.parameters != null
                ? selectedExpParams.parameters.ToList()
                : new List<VRCExpressionParameters.Parameter>();

        if (list.Any(p => p != null && p.name == paramName))
        {
            return;
        }

        list.Add(
            new VRCExpressionParameters.Parameter
            {
                name = paramName,
                valueType = VRCExpressionParameters.ValueType.Float,
                saved = false,
                defaultValue = 0f,
                networkSynced = false
            }
        );

        selectedExpParams.parameters = list.ToArray();
        EditorUtility.SetDirty(selectedExpParams);
    }

    private void AddSyncedBoolParameter(string paramName, bool defaultValue)
    {
        if (selectedExpParams == null)
        {
            return;
        }

        List<VRCExpressionParameters.Parameter> list =
            selectedExpParams.parameters != null
                ? selectedExpParams.parameters.ToList()
                : new List<VRCExpressionParameters.Parameter>();

        if (list.Any(p => p != null && p.name == paramName))
        {
            return;
        }

        list.Add(
            new VRCExpressionParameters.Parameter
            {
                name = paramName,
                valueType = VRCExpressionParameters.ValueType.Bool,
                saved = false,
                defaultValue = defaultValue ? 1f : 0f,
                networkSynced = true
            }
        );

        selectedExpParams.parameters = list.ToArray();
        EditorUtility.SetDirty(selectedExpParams);
    }

    private void EnsureAnimatorFloatParameter(string paramName)
    {
        if (targetController == null)
        {
            return;
        }

        if (targetController.parameters.Any(p => p.name == paramName))
        {
            return;
        }

        targetController.AddParameter(
            paramName,
            AnimatorControllerParameterType.Float
        );
    }

    private void EnsureAnimatorBoolParameter(string paramName)
    {
        if (targetController == null)
        {
            return;
        }

        if (targetController.parameters.Any(p => p.name == paramName))
        {
            return;
        }

        targetController.AddParameter(
            paramName,
            AnimatorControllerParameterType.Bool
        );
    }

    private void RemoveExistingLayer(string layerName)
    {
        if (targetController == null)
        {
            return;
        }

        List<AnimatorControllerLayer> layers =
            targetController.layers.ToList();

        int index =
            layers.FindIndex(l => l.name == layerName);

        if (index < 0)
        {
            return;
        }

        AnimatorControllerLayer layer = layers[index];
        if (layer.stateMachine != null)
        {
            DestroyStateMachineRecursive(layer.stateMachine);
        }

        layers.RemoveAt(index);
        targetController.layers = layers.ToArray();
        EditorUtility.SetDirty(targetController);
    }

    private void DestroyStateMachineRecursive(AnimatorStateMachine sm)
    {
        if (sm == null)
        {
            return;
        }

        foreach (ChildAnimatorState state in sm.states)
        {
            if (state.state != null)
            {
                if (state.state.motion != null)
                {
                    DestroyMotionRecursive(state.state.motion);
                }

                foreach (
                    StateMachineBehaviour behaviour
                    in state.state.behaviours)
                {
                    if (behaviour != null)
                    {
                        Object.DestroyImmediate(behaviour, true);
                    }
                }

                Object.DestroyImmediate(state.state, true);
            }
        }

        foreach (ChildAnimatorStateMachine childSm in sm.stateMachines)
        {
            if (childSm.stateMachine != null)
            {
                DestroyStateMachineRecursive(childSm.stateMachine);
            }
        }

        Object.DestroyImmediate(sm, true);
    }

    private AnimationClip CreateWorldRelativeRotationClip(
        string clipName,
        Transform bone,
        float pitchDegrees,
        float yawDegrees)
    {
        AnimationClip clip = new AnimationClip { name = clipName };
        string relativePath = GetRelativePath(bone);

        Quaternion relativeRot =
            Quaternion.Euler(pitchDegrees, yawDegrees, 0f);

        clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localRotation.x",
            AnimationCurve.Constant(0f, 1f / 60f, relativeRot.x)
        );

        clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localRotation.y",
            AnimationCurve.Constant(0f, 1f / 60f, relativeRot.y)
        );

        clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localRotation.z",
            AnimationCurve.Constant(0f, 1f / 60f, relativeRot.z)
        );

        clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localRotation.w",
            AnimationCurve.Constant(0f, 1f / 60f, relativeRot.w)
        );

        return SaveClipAsset(clip, clipName);
    }

    private AnimationClip CreateVerticalClip(
        string clipName,
        List<Transform> bones,
        float yOffset)
    {
        AnimationClip clip = new AnimationClip { name = clipName };

        foreach (Transform bone in bones)
        {
            if (bone == null) continue;

            string relativePath = GetRelativePath(bone);
            Vector3 localPos = bone.localPosition;
            Vector3 targetPos = localPos + new Vector3(0f, yOffset, 0f);

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Constant(0f, 1f / 60f, targetPos.x)
            );

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localPosition.y",
                AnimationCurve.Constant(0f, 1f / 60f, targetPos.y)
            );

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localPosition.z",
                AnimationCurve.Constant(0f, 1f / 60f, targetPos.z)
            );
        }

        return SaveClipAsset(clip, clipName);
    }

    private static string MakeYawLabel(
        int stepIndex,
        int totalSteps,
        float normalizedYaw)
    {
        int midIndex = totalSteps / 2;

        if (stepIndex == midIndex)
        {
            return "YawCenter";
        }

        if (stepIndex < midIndex)
        {
            int index = midIndex - stepIndex;
            return "YawNeg" + index;
        }
        else
        {
            int index = stepIndex - midIndex;
            return "YawPos" + index;
        }
    }

    private string GetRelativePath(Transform target)
    {
        if (target == null) return "";

        Animator[] animators = FindObjectsOfType<Animator>();
        Animator avatarAnimator =
            animators.FirstOrDefault(a => a.isHuman) ?? animators[0];

        Transform root = avatarAnimator != null ? avatarAnimator.transform : null;

        if (root == null || target == root) return "";

        List<string> pathParts = new List<string>();
        Transform current = target;

        while (current != null && current != root)
        {
            pathParts.Add(current.name);
            current = current.parent;
        }

        pathParts.Reverse();
        return string.Join("/", pathParts);
    }

    private AnimationClip SaveClipAsset(
        AnimationClip clip,
        string clipName)
    {
        EnsureFolder(clipOutputFolder);
        string fullPath =
            Path.Combine(clipOutputFolder, clipName + ".anim")
                .Replace("\\", "/");

        AnimationClip existing =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);

        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(clip);
        }
        else
        {
            AssetDatabase.CreateAsset(clip, fullPath);
        }

        AssetDatabase.ImportAsset(
            fullPath,
            ImportAssetOptions.ForceUpdate
        );

        return existing != null
            ? existing
            : AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
    }

    private void EnsureFolder(string folder)
    {
        folder = folder.Replace("\\", "/").TrimEnd('/');

        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        if (!folder.StartsWith("Assets"))
        {
            Debug.LogError(
                $"[EarRig] Folder must be inside Assets: {folder}"
            );
            return;
        }

        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private void AutoFillEarBones()
    {
        Animator[] animators = FindObjectsOfType<Animator>();

        if (animators.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Auto Fill Failed",
                "No Animator was found in the scene.",
                "OK"
            );
            return;
        }

        Animator avatarAnimator =
            animators.FirstOrDefault(a => a.isHuman) ?? animators[0];

        Transform avatarRoot = avatarAnimator.transform;

        Transform[] transforms =
            avatarRoot.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in transforms)
        {
            string normalized =
                t.name
                    .ToLowerInvariant()
                    .Replace(" ", "")
                    .Replace("_", "")
                    .Replace("-", "")
                    .Replace(".", "");

            bool earLike = normalized.Contains("ear");

            if (!earLike)
            {
                continue;
            }

            bool left =
                normalized.Contains("left") ||
                normalized.EndsWith("l") ||
                normalized.StartsWith("l");

            bool right =
                normalized.Contains("right") ||
                normalized.EndsWith("r") ||
                normalized.StartsWith("r");

            if (left && leftEarBone == null)
            {
                leftEarBone = t;

                Debug.Log(
                    $"[EarRig] Auto-mapped {t.name} -> Left Ear"
                );
            }
            else if (right && rightEarBone == null)
            {
                rightEarBone = t;

                Debug.Log(
                    $"[EarRig] Auto-mapped {t.name} -> Right Ear"
                );
            }
        }

        if (leftEarBone == null && rightEarBone == null)
        {
            EditorUtility.DisplayDialog(
                "Auto Fill Results",
                "No matching ear bones were automatically detected.",
                "OK"
            );
        }
    }

    private static void SaveAssetPreference<T>(string key, T asset) where T : Object
    {
        if (asset == null)
        {
            EditorPrefs.DeleteKey(key);
            return;
        }

        string path = AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrEmpty(path))
        {
            EditorPrefs.DeleteKey(key);
            return;
        }

        EditorPrefs.SetString(key, path);
    }

    private static T LoadAssetPreference<T>(string key) where T : Object
    {
        string path = EditorPrefs.GetString(key, "");

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static void SaveTransformPreference(string key, Transform transform)
    {
        if (transform == null)
        {
            EditorPrefs.DeleteKey(key);
            return;
        }

        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(transform);
        EditorPrefs.SetString(key, id.ToString());
    }

    private static Transform LoadTransformPreference(string key)
    {
        string serializedId = EditorPrefs.GetString(key, "");

        if (string.IsNullOrEmpty(serializedId))
        {
            return null;
        }

        if (!GlobalObjectId.TryParse(serializedId, out GlobalObjectId id))
        {
            return null;
        }

        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Transform;
    }
}
