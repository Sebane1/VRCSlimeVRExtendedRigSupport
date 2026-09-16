// TailRigInjector.cs - Dynamic Tail Control & Codec Injector for VRChat

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using VRC.SDK3.Dynamics.PhysBone.Components;

public class TailRigInjector : EditorWindow
{
    private AnimatorController targetController;
    private VRCExpressionParameters selectedExpParams;
    private Transform tailRootBone;

    private string clipOutputFolder = "Assets/Animations/TailTracking";

    private float maxPitch = 90f;
    private float maxYaw = 90f;

    private float pitchSensitivity = 1.0f;
    private float yawSensitivity = 1.0f;

    private float neutralPitchOffset = 0f;

    private bool useOSCSmoothPath;

    private Vector2 scroll;

    private const string TailPitch = "TailPitchFloat";
    private const string TailYaw = "TailYawFloat";

    private const int EncodedLevels = 15;
    private const int EncodedMiddle = 7;
    private const int NeutralGrayCode = 4;

    private const string PrefPrefix = "VRCSlimeVRTailRigSupport_";

    [MenuItem("Tools/Tail Rig/Add Tail Tracking Compatibility")]
    public static void Open()
    {
        GetWindow<TailRigInjector>("Tail Tracking Configurator");
    }

    private void OnEnable()
    {
        useOSCSmoothPath = EditorPrefs.GetBool(
            PrefPrefix + "UseOSCSmooth",
            false
        );

        clipOutputFolder = EditorPrefs.GetString(
            PrefPrefix + "ClipOutputFolder",
            "Assets/Animations/TailTracking"
        );

        pitchSensitivity = EditorPrefs.GetFloat(
            PrefPrefix + "PitchSensitivity",
            1.0f
        );

        yawSensitivity = EditorPrefs.GetFloat(
            PrefPrefix + "YawSensitivity",
            1.0f
        );

        neutralPitchOffset = EditorPrefs.GetFloat(
            PrefPrefix + "NeutralPitchOffset",
            0f
        );

        targetController =
            LoadAssetPreference<AnimatorController>(
                PrefPrefix + "TargetController"
            );

        selectedExpParams =
            LoadAssetPreference<VRCExpressionParameters>(
                PrefPrefix + "ExpressionParameters"
            );

        tailRootBone =
            LoadTransformPreference(
                PrefPrefix + "TailRootBone"
            );
    }

    private void OnDisable()
    {
        EditorPrefs.SetBool(
            PrefPrefix + "UseOSCSmooth",
            useOSCSmoothPath
        );

        EditorPrefs.SetString(
            PrefPrefix + "ClipOutputFolder",
            clipOutputFolder
        );

        EditorPrefs.SetFloat(
            PrefPrefix + "PitchSensitivity",
            pitchSensitivity
        );

        EditorPrefs.SetFloat(
            PrefPrefix + "YawSensitivity",
            yawSensitivity
        );

        EditorPrefs.SetFloat(
            PrefPrefix + "NeutralPitchOffset",
            neutralPitchOffset
        );

        SaveAssetPreference(
            PrefPrefix + "TargetController",
            targetController
        );

        SaveAssetPreference(
            PrefPrefix + "ExpressionParameters",
            selectedExpParams
        );

        SaveTransformPreference(
            PrefPrefix + "TailRootBone",
            tailRootBone
        );
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "SlimeVR Tail Tracking Configurator",
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
            "Tail Root Bone",
            EditorStyles.boldLabel
        );

        tailRootBone =
            (Transform)EditorGUILayout.ObjectField(
                "Tail Root",
                tailRootBone,
                typeof(Transform),
                true
            );

        if (tailRootBone != null)
        {
            List<Transform> chain =
                GetTailChain(tailRootBone);

            VRCPhysBone[] physBones =
                tailRootBone.GetComponentsInChildren<VRCPhysBone>(true);

            EditorGUILayout.HelpBox(
                $"Detected {chain.Count} bone(s) in tail chain starting at '{tailRootBone.name}'.\n" +
                $"Detected {physBones.Length} PhysBone component(s) under the selected tail root.",
                MessageType.Info
            );
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Rotation Settings",
            EditorStyles.boldLabel
        );

        pitchSensitivity =
            EditorGUILayout.Slider(
                "Pitch Sensitivity",
                pitchSensitivity,
                0.1f,
                10.0f
            );

        yawSensitivity =
            EditorGUILayout.Slider(
                "Yaw Sensitivity",
                yawSensitivity,
                0.1f,
                10.0f
            );

        neutralPitchOffset =
            EditorGUILayout.Slider(
                "Neutral Tail Pitch Offset",
                neutralPitchOffset,
                -90f,
                90f
            );

        EditorGUILayout.HelpBox(
            "Higher sensitivity increases the raw tracker range that is distributed across tail joints.\r\n\r\n" +
            "Neutral Tail Pitch Offset changes the resting angle of the tail relative to any tracker motion.",
            MessageType.Info
        );

        EditorGUILayout.Space();

        float pitchCodecRange =
            1f / Mathf.Max(
                1f,
                pitchSensitivity
            );

        float yawCodecRange =
            1f / Mathf.Max(
                1f,
                yawSensitivity
            );

        EditorGUILayout.Space();

        GUI.enabled =
            targetController != null &&
            selectedExpParams != null &&
            tailRootBone != null;

        if (GUILayout.Button("Generate Tail Support"))
        {
            Generate();
        }

        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    private List<Transform> GetTailChain(Transform root)
    {
        List<Transform> chain =
            new List<Transform>();

        Transform current = root;

        while (current != null)
        {
            chain.Add(current);

            if (current.childCount > 0)
            {
                current =
                    current.GetChild(0);
            }
            else
            {
                break;
            }
        }

        return chain;
    }

    private int EnableAnimatedPhysBones(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        VRCPhysBone[] physBones =
            root.GetComponentsInChildren<VRCPhysBone>(true);

        int changedCount = 0;

        foreach (VRCPhysBone physBone in physBones)
        {
            if (physBone == null)
            {
                continue;
            }

            if (!physBone.isAnimated)
            {
                Undo.RecordObject(
                    physBone,
                    "Enable Is Animated For Tail PhysBone"
                );

                physBone.isAnimated = true;

                EditorUtility.SetDirty(
                    physBone
                );

                changedCount++;
            }
        }

        return changedCount;
    }

    private void Generate()
    {
        if (
            targetController == null ||
            selectedExpParams == null ||
            tailRootBone == null
        )
        {
            return;
        }

        EnsureDirectoryExists(
            clipOutputFolder
        );

        List<Transform> chain =
            GetTailChain(tailRootBone);

        VRCPhysBone[] detectedPhysBones =
            tailRootBone.GetComponentsInChildren<VRCPhysBone>(true);

        int changedPhysBoneCount =
            EnableAnimatedPhysBones(tailRootBone);

        EnsureAnimatorBoolParameter(
            "IsLocal"
        );

        AddUnsyncedFloatParameter(
            TailPitch
        );

        AddUnsyncedFloatParameter(
            TailYaw
        );

        AddEncodedBoolParameters(
            TailPitch
        );

        AddEncodedBoolParameters(
            TailYaw
        );

        RemoveExistingLayer(
            "Tail"
        );

        RemoveExistingLayer(
            "Tail_Pitch4BitEncode"
        );

        RemoveExistingLayer(
            "Tail_Yaw4BitEncode"
        );

        RemoveExistingLayer(
            "Tail_Pitch4BitDecode"
        );

        RemoveExistingLayer(
            "Tail_Yaw4BitDecode"
        );

        string controllerPath =
            AssetDatabase.GetAssetPath(
                targetController
            );

        GenerateFourBitEncoderLayer(
            "Tail_Pitch4BitEncode",
            TailPitch,
            controllerPath,
            pitchSensitivity
        );

        GenerateFourBitEncoderLayer(
            "Tail_Yaw4BitEncode",
            TailYaw,
            controllerPath,
            yawSensitivity
        );

        GenerateFourBitDecoderLayer(
            "Tail_Pitch4BitDecode",
            TailPitch,
            controllerPath,
            pitchSensitivity
        );

        GenerateFourBitDecoderLayer(
            "Tail_Yaw4BitDecode",
            TailYaw,
            controllerPath,
            yawSensitivity
        );

        GenerateTailMotionLayer(
            chain,
            controllerPath
        );

        EditorUtility.SetDirty(
            selectedExpParams
        );

        EditorUtility.SetDirty(
            targetController
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Tail Configuration Complete!",
            $"Tail tracking support created successffully!",
            "OK"
        );
    }

    private void GenerateTailMotionLayer(
        List<Transform> chain,
        string controllerPath
    )
    {
        string resolvedPitch =
            useOSCSmoothPath
                ? "OSCm/Proxy/" + TailPitch
                : TailPitch;

        string resolvedYaw =
            useOSCSmoothPath
                ? "OSCm/Proxy/" + TailYaw
                : TailYaw;

        EnsureAnimatorFloatParameter(
            resolvedPitch
        );

        EnsureAnimatorFloatParameter(
            resolvedYaw
        );

        var stateMachine =
            new AnimatorStateMachine
            {
                name = "Tail"
            };

        AssetDatabase.AddObjectToAsset(
            stateMachine,
            controllerPath
        );

        var yawTree =
            new BlendTree
            {
                name =
                    "Tail_YawPitch_BlendTree",

                blendType =
                    BlendTreeType.Simple1D,

                useAutomaticThresholds =
                    false,

                blendParameter =
                    resolvedYaw
            };

        AssetDatabase.AddObjectToAsset(
            yawTree,
            controllerPath
        );

        int pitchSteps =
            EncodedLevels;

        int yawSteps =
            EncodedLevels;

        float yawCodecScale =
            Mathf.Max(
                1f,
                yawSensitivity
            );

        float pitchCodecScale =
            Mathf.Max(
                1f,
                pitchSensitivity
            );

        for (
            int i = 0;
            i < yawSteps;
            i++
        )
        {
            float normalizedYaw =
                (float)i /
                (yawSteps - 1) *
                2f -
                1f;

            float rawYaw =
                normalizedYaw /
                yawCodecScale;

            float yawDegrees =
                Mathf.Clamp(
                    -rawYaw *
                    yawSensitivity *
                    maxYaw,
                    -maxYaw,
                    maxYaw
                );

            var pitchTree =
                new BlendTree
                {
                    name =
                        $"Tail_Yaw_{i:00}_PitchTree",

                    blendType =
                        BlendTreeType.Simple1D,

                    useAutomaticThresholds =
                        false,

                    blendParameter =
                        resolvedPitch
                };

            AssetDatabase.AddObjectToAsset(
                pitchTree,
                controllerPath
            );

            for (
                int j = 0;
                j < pitchSteps;
                j++
            )
            {
                float normalizedPitch =
                    (float)j /
                    (pitchSteps - 1) *
                    2f -
                    1f;

                float rawPitch =
                    normalizedPitch /
                    pitchCodecScale;

                float pitchDegrees =
                    Mathf.Clamp(
                        rawPitch *
                        pitchSensitivity *
                        maxPitch,
                        -maxPitch,
                        maxPitch
                    );

                AnimationClip pitchClip =
                    CreateTailChainPoseClip(
                        $"Tail_P_{j:00}_Y_{i:00}",
                        chain,
                        pitchDegrees,
                        yawDegrees
                    );

                pitchTree.AddChild(
                    pitchClip,
                    rawPitch
                );
            }

            yawTree.AddChild(
                pitchTree,
                rawYaw
            );
        }

        AnimatorState state =
            stateMachine.AddState(
                "Tracking"
            );

        state.motion =
            yawTree;

        state.writeDefaultValues =
            true;

        stateMachine.defaultState =
            state;

        var layer =
            new AnimatorControllerLayer
            {
                name = "Tail",
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

        var layers =
            targetController.layers.ToList();

        layers.Add(
            layer
        );

        targetController.layers =
            layers.ToArray();
    }

    private AnimationClip CreateTailChainPoseClip(
        string name,
        List<Transform> chain,
        float pitchDegrees,
        float yawDegrees
    )
    {
        var clip =
            new AnimationClip
            {
                name = name,
                frameRate = 60f,
                wrapMode = WrapMode.Loop
            };

        Transform avatarRoot =
            GetAvatarRoot(
                chain[0]
            );

        Vector3 avatarRight =
            avatarRoot != null
                ? avatarRoot.right.normalized
                : Vector3.right;

        Vector3 avatarUp =
            avatarRoot != null
                ? avatarRoot.up.normalized
                : Vector3.up;

        Quaternion[] originalRotations =
            chain
                .Select(
                    t => t.rotation
                )
                .ToArray();

        Vector3[] originalEulerAngles =
            chain
                .Select(
                    t => t.localEulerAngles
                )
                .ToArray();

        try
        {
            float[] pitchScaleFactors =
            {
                1.2f,
                1.3f,
                1.4f,
                1.3f,
                1.1f,
                0.9f
            };

            float[] yawScaleFactors =
            {
                2.0f,
                2.3f,
                2.5f,
                2.3f,
                2.0f,
                1.6f
            };

            float[] neutralPitchScaleFactors =
            {
                2.0f,
                1.6f,
                1.2f,
                0.8f,
                0.4f,
                0.2f
            };

            for (
                int i = 0;
                i < chain.Count;
                i++
            )
            {
                chain[i].rotation =
                    originalRotations[i];
            }

            for (
                int i = 0;
                i < chain.Count;
                i++
            )
            {
                Transform bone =
                    chain[i];

                string path =
                    GetBonePath(
                        bone
                    );

                if (
                    string.IsNullOrEmpty(
                        path
                    )
                )
                {
                    continue;
                }

                float pitchScale =
                    i < pitchScaleFactors.Length
                        ? pitchScaleFactors[i]
                        : 1.0f + 0.1f * i;

                float yawScale =
                    i < yawScaleFactors.Length
                        ? yawScaleFactors[i]
                        : 1.5f + 0.2f * i;

                float neutralPitchScale =
                    i < neutralPitchScaleFactors.Length
                        ? neutralPitchScaleFactors[i]
                        : 0.2f;

                float trackedPitch =
                    -(
                        (pitchDegrees / chain.Count) *
                        pitchScale
                    );

                float bakedNeutralPitch =
                    -(
                        (neutralPitchOffset / chain.Count) *
                        neutralPitchScale
                    );

                float stepPitch =
                    trackedPitch +
                    bakedNeutralPitch;

                float stepYaw =
                    (yawDegrees / chain.Count) *
                    yawScale;

                bone.Rotate(
                    avatarUp,
                    stepYaw,
                    Space.World
                );

                bone.Rotate(
                    avatarRight,
                    stepPitch,
                    Space.World
                );

                Vector3 localEuler =
                    GetContinuousEuler(
                        originalEulerAngles[i],
                        bone.localEulerAngles
                    );

                SetConstantCurve(
                    clip,
                    path,
                    "localEulerAnglesRaw.x",
                    localEuler.x
                );

                SetConstantCurve(
                    clip,
                    path,
                    "localEulerAnglesRaw.y",
                    localEuler.y
                );

                SetConstantCurve(
                    clip,
                    path,
                    "localEulerAnglesRaw.z",
                    localEuler.z
                );
            }
        }
        finally
        {
            for (
                int i = 0;
                i < chain.Count;
                i++
            )
            {
                chain[i].rotation =
                    originalRotations[i];
            }
        }

        string clipPath =
            $"{clipOutputFolder}/{name}.anim";

        return SaveOrOverwriteClip(
            clip,
            clipPath
        );
    }

    private static void SetConstantCurve(
        AnimationClip clip,
        string path,
        string propertyName,
        float value
    )
    {
        clip.SetCurve(
            path,
            typeof(Transform),
            propertyName,
            AnimationCurve.Constant(
                0f,
                1f / 60f,
                value
            )
        );
    }

    private static Vector3 GetContinuousEuler(
        Vector3 reference,
        Vector3 current
    )
    {
        return new Vector3(
            reference.x +
            Mathf.DeltaAngle(
                reference.x,
                current.x
            ),

            reference.y +
            Mathf.DeltaAngle(
                reference.y,
                current.y
            ),

            reference.z +
            Mathf.DeltaAngle(
                reference.z,
                current.z
            )
        );
    }

    private Transform GetAvatarRoot(
        Transform bone
    )
    {
        VRCAvatarDescriptor descriptor =
            bone.GetComponentInParent<VRCAvatarDescriptor>();

        return descriptor != null
            ? descriptor.transform
            : null;
    }

    private string GetBonePath(
        Transform bone
    )
    {
        Transform root =
            GetAvatarRoot(
                bone
            );

        if (
            root == null ||
            bone == root
        )
        {
            return "";
        }

        List<string> pathParts =
            new List<string>();

        Transform current =
            bone;

        while (
            current != null &&
            current != root
        )
        {
            pathParts.Add(
                current.name
            );

            current =
                current.parent;
        }

        pathParts.Reverse();

        return string.Join(
            "/",
            pathParts
        );
    }

    private void EnsureDirectoryExists(
        string folderPath
    )
    {
        if (
            !Directory.Exists(
                folderPath
            )
        )
        {
            Directory.CreateDirectory(
                folderPath
            );
        }
    }

    private AnimationClip SaveOrOverwriteClip(
        AnimationClip newClip,
        string path
    )
    {
        AnimationClip existing =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(
                path
            );

        if (
            existing != null
        )
        {
            EditorUtility.CopySerialized(
                newClip,
                existing
            );

            EditorUtility.SetDirty(
                existing
            );

            return existing;
        }

        AssetDatabase.CreateAsset(
            newClip,
            path
        );

        return newClip;
    }

    private void RemoveExistingLayer(
        string layerName
    )
    {
        int index =
            targetController.layers
                .ToList()
                .FindIndex(
                    layer =>
                        layer.name == layerName
                );

        if (
            index >= 0
        )
        {
            targetController.RemoveLayer(
                index
            );
        }
    }

    private void AddUnsyncedFloatParameter(
        string parameter
    )
    {
        if (
            !targetController.parameters.Any(
                p => p.name == parameter
            )
        )
        {
            targetController.AddParameter(
                parameter,
                AnimatorControllerParameterType.Float
            );
        }

        VRCExpressionUtility.AddMissingParameter(
            selectedExpParams,
            parameter,
            VRCExpressionParameters.ValueType.Float,
            false,
            0,
            false
        );
    }

    private void AddEncodedBoolParameters(
        string parameter
    )
    {
        for (
            int i = 0;
            i < 4;
            i++
        )
        {
            string bitName =
                $"{parameter}_B{i}";

            EnsureAnimatorBoolParameter(
                bitName
            );

            var expressionParameters =
                selectedExpParams.parameters?.ToList()
                ??
                new List<VRCExpressionParameters.Parameter>();

            int index =
                expressionParameters.FindIndex(
                    p =>
                        p.name == bitName
                );

            VRCExpressionParameters.Parameter param =
                index >= 0
                    ? expressionParameters[index]
                    : new VRCExpressionParameters.Parameter
                    {
                        name = bitName
                    };

            param.valueType =
                VRCExpressionParameters.ValueType.Bool;

            param.networkSynced =
                true;

            param.saved =
                false;

            param.defaultValue =
                0f;

            if (
                index >= 0
            )
            {
                expressionParameters[index] =
                    param;
            }
            else
            {
                expressionParameters.Add(
                    param
                );
            }

            selectedExpParams.parameters =
                expressionParameters.ToArray();
        }
    }

    private void EnsureAnimatorFloatParameter(
        string parameter
    )
    {
        if (
            !targetController.parameters.Any(
                p => p.name == parameter
            )
        )
        {
            targetController.AddParameter(
                parameter,
                AnimatorControllerParameterType.Float
            );
        }
    }

    private void EnsureAnimatorBoolParameter(
        string parameter
    )
    {
        AnimatorControllerParameter[] matches =
            targetController.parameters
                .Where(
                    p => p.name == parameter
                )
                .ToArray();

        if (
            matches.Length == 1 &&
            matches[0].type ==
                AnimatorControllerParameterType.Bool
        )
        {
            return;
        }

        while (
            targetController.parameters.Any(
                p => p.name == parameter
            )
        )
        {
            AnimatorControllerParameter existing =
                targetController.parameters.First(
                    p => p.name == parameter
                );

            targetController.RemoveParameter(
                existing
            );
        }

        targetController.AddParameter(
            parameter,
            AnimatorControllerParameterType.Bool
        );

        EditorUtility.SetDirty(
            targetController
        );
    }

    private static int QuantizedIndexToWireCode(
        int index
    )
    {
        index =
            Mathf.Clamp(
                index,
                0,
                EncodedLevels - 1
            );

        int gray =
            index ^
            (index >> 1);

        return gray ^
               NeutralGrayCode;
    }

    private static int WireCodeToQuantizedIndex(
        int wireCode
    )
    {
        int gray =
            wireCode ^
            NeutralGrayCode;

        int binary =
            0;

        for (
            int value = gray;
            value != 0;
            value >>= 1
        )
        {
            binary ^=
                value;
        }

        return binary;
    }

    private static void AddForcedBoolCondition(
        AnimatorStateTransition transition,
        string parameterName,
        bool expectedValue
    )
    {
        AnimatorCondition[] retainedConditions =
            transition.conditions
                .Where(
                    condition =>
                        condition.parameter != parameterName
                )
                .ToArray();

        transition.conditions =
            retainedConditions;

        transition.AddCondition(
            expectedValue
                ? AnimatorConditionMode.If
                : AnimatorConditionMode.IfNot,
            0f,
            parameterName
        );

        EditorUtility.SetDirty(
            transition
        );
    }

    private void GenerateFourBitEncoderLayer(
        string layerName,
        string rawSourceFloatParameter,
        string controllerPath,
        float sensitivity
    )
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

        AnimatorState idleState =
            stateMachine.AddState(
                "Remote_Idle",
                new Vector3(
                    0f,
                    0f,
                    0f
                )
            );

        idleState.writeDefaultValues =
            true;

        stateMachine.defaultState =
            idleState;

        float codecScale =
            Mathf.Max(
                1f,
                sensitivity
            );

        for (
            int qIndex = 0;
            qIndex < EncodedLevels;
            qIndex++
        )
        {
            int wireCode =
                QuantizedIndexToWireCode(
                    qIndex
                );

            AnimatorState state =
                stateMachine.AddState(
                    $"Q{qIndex:00}_Code{wireCode:X1}",
                    new Vector3(
                        280f *
                        (qIndex % 4),

                        100f +
                        90f *
                        (qIndex / 4),

                        0f
                    )
                );

            state.writeDefaultValues =
                true;

            var driver =
                state.AddStateMachineBehaviour<
                    VRCAvatarParameterDriver
                >();

            driver.localOnly =
                false;

            driver.parameters =
                new List<
                    VRC_AvatarParameterDriver.Parameter
                >();

            for (
                int bit = 0;
                bit < 4;
                bit++
            )
            {
                bool bitSet =
                    (
                        wireCode &
                        (1 << bit)
                    ) != 0;

                driver.parameters.Add(
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        name =
                            $"{rawSourceFloatParameter}_B{bit}",

                        type =
                            VRC_AvatarParameterDriver.ChangeType.Set,

                        value =
                            bitSet
                                ? 1f
                                : 0f
                    }
                );
            }

            AnimatorStateTransition transition =
                stateMachine.AddAnyStateTransition(
                    state
                );

            transition.hasExitTime =
                false;

            transition.duration =
                0f;

            transition.canTransitionToSelf =
                false;

            AddForcedBoolCondition(
                transition,
                "IsLocal",
                true
            );

            if (
                qIndex > 0
            )
            {
                float normalizedLowerBoundary =
                    -1f +
                    (qIndex - 0.5f) /
                    EncodedMiddle;

                float rawLowerBoundary =
                    normalizedLowerBoundary /
                    codecScale;

                transition.AddCondition(
                    AnimatorConditionMode.Greater,
                    rawLowerBoundary,
                    rawSourceFloatParameter
                );
            }

            if (
                qIndex <
                EncodedLevels - 1
            )
            {
                float normalizedUpperBoundary =
                    -1f +
                    (qIndex + 0.5f) /
                    EncodedMiddle;

                float rawUpperBoundary =
                    normalizedUpperBoundary /
                    codecScale;

                transition.AddCondition(
                    AnimatorConditionMode.Less,
                    rawUpperBoundary,
                    rawSourceFloatParameter
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

        layers.Add(
            layer
        );

        targetController.layers =
            layers.ToArray();
    }

    private void GenerateFourBitDecoderLayer(
        string layerName,
        string logicalParameter,
        string controllerPath,
        float sensitivity
    )
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

        AnimatorState idleState =
            stateMachine.AddState(
                "Local_Idle",
                new Vector3(
                    0f,
                    0f,
                    0f
                )
            );

        idleState.writeDefaultValues =
            true;

        stateMachine.defaultState =
            idleState;

        float codecScale =
            Mathf.Max(
                1f,
                sensitivity
            );

        for (
            int wireCode = 0;
            wireCode < 16;
            wireCode++
        )
        {
            int quantizedIndex =
                WireCodeToQuantizedIndex(
                    wireCode
                );

            bool valid =
                quantizedIndex >= 0 &&
                quantizedIndex <
                EncodedLevels;

            float normalizedValue =
                valid
                    ? (
                        quantizedIndex -
                        EncodedMiddle
                    ) /
                    (float)EncodedMiddle
                    : 0f;

            float decodedValue =
                normalizedValue /
                codecScale;

            string stateName =
                valid
                    ? $"Code_{wireCode:X1}_Step_{quantizedIndex:00}"
                    : $"Code_{wireCode:X1}_FallbackNeutral";

            AnimatorState state =
                stateMachine.AddState(
                    stateName,
                    new Vector3(
                        260f *
                        (wireCode % 4),

                        100f +
                        90f *
                        (wireCode / 4),

                        0f
                    )
                );

            state.writeDefaultValues =
                true;

            var driver =
                state.AddStateMachineBehaviour<
                    VRCAvatarParameterDriver
                >();

            driver.localOnly =
                false;

            driver.parameters =
                new List<
                    VRC_AvatarParameterDriver.Parameter
                >
                {
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        name =
                            logicalParameter,

                        type =
                            VRC_AvatarParameterDriver.ChangeType.Set,

                        value =
                            decodedValue
                    }
                };

            AnimatorStateTransition transition =
                stateMachine.AddAnyStateTransition(
                    state
                );

            transition.hasExitTime =
                false;

            transition.duration =
                0f;

            transition.canTransitionToSelf =
                false;

            AddForcedBoolCondition(
                transition,
                "IsLocal",
                false
            );

            for (
                int bit = 0;
                bit < 4;
                bit++
            )
            {
                bool bitSet =
                    (
                        wireCode &
                        (1 << bit)
                    ) != 0;

                transition.AddCondition(
                    bitSet
                        ? AnimatorConditionMode.If
                        : AnimatorConditionMode.IfNot,
                    0f,
                    $"{logicalParameter}_B{bit}"
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

        layers.Add(
            layer
        );

        targetController.layers =
            layers.ToArray();
    }

    private T LoadAssetPreference<T>(
        string key
    )
        where T : UnityEngine.Object
    {
        string path =
            EditorPrefs.GetString(
                key,
                ""
            );

        return string.IsNullOrEmpty(
            path
        )
            ? null
            : AssetDatabase.LoadAssetAtPath<T>(
                path
            );
    }

    private void SaveAssetPreference<T>(
        string key,
        T asset
    )
        where T : UnityEngine.Object
    {
        EditorPrefs.SetString(
            key,
            asset != null
                ? AssetDatabase.GetAssetPath(
                    asset
                )
                : ""
        );
    }

    private Transform LoadTransformPreference(
        string key
    )
    {
        string path =
            EditorPrefs.GetString(
                key,
                ""
            );

        if (
            string.IsNullOrEmpty(
                path
            )
        )
        {
            return null;
        }

        GameObject go =
            GameObject.Find(
                path
            );

        return go != null
            ? go.transform
            : null;
    }

    private void SaveTransformPreference(
        string key,
        Transform transform
    )
    {
        EditorPrefs.SetString(
            key,
            transform != null
                ? GetGameObjectPath(
                    transform
                )
                : ""
        );
    }

    private string GetGameObjectPath(
        Transform transform
    )
    {
        List<string> pathParts =
            new List<string>();

        Transform current =
            transform;

        while (
            current != null
        )
        {
            pathParts.Add(
                current.name
            );

            current =
                current.parent;
        }

        pathParts.Reverse();

        return string.Join(
            "/",
            pathParts
        );
    }
}