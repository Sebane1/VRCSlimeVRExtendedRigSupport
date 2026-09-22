using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

public class PosteriorRigInjector : EditorWindow
{
    private AnimatorController targetController;
    private VRCExpressionParameters selectedExpParams;

    private Transform leftPosteriorBone;
    private Transform rightPosteriorBone;

    private string clipOutputFolder = "Assets/Animations/PosteriorTracking";

    private float maxPitch = -90f;
    private float maxYaw = -90f;
    private float maxVertical = 0.01f;
    private int yawSteps = 15;
    private bool useOSCSmoothPath;

    private Vector2 scroll;

    private const string LeftPitch = "LeftPosteriorPitch";
    private const string LeftYaw = "LeftPosteriorYaw";
    private const string RightPitch = "RightPosteriorPitch";
    private const string RightYaw = "RightPosteriorYaw";
    private const string PosteriorVertical = "PosteriorVertical";

    private const int EncodedLevels = 15;
    private const int EncodedMiddle = 7;
    private const int NeutralGrayCode = 4;


    private const string PrefPrefix = "VRCSlimeVRExtendedRigSupport_Posterior_";

    [MenuItem("Tools/Posterior Rig/Add Posterior Tracking Compatibility")]
    public static void Open()
    {
        GetWindow<PosteriorRigInjector>("Posterior Tracking Configurator");
    }

    private void OnEnable()
    {
        useOSCSmoothPath = EditorPrefs.GetBool(PrefPrefix + "UseOSCSmooth", false);

        clipOutputFolder = EditorPrefs.GetString(
            PrefPrefix + "ClipOutputFolder",
            "Assets/Animations/PosteriorTracking"
        );

        targetController = LoadAssetPreference<AnimatorController>(
            PrefPrefix + "TargetController"
        );

        selectedExpParams = LoadAssetPreference<VRCExpressionParameters>(
            PrefPrefix + "ExpressionParameters"
        );

        leftPosteriorBone = LoadTransformPreference(PrefPrefix + "LeftPosteriorBone");
        rightPosteriorBone = LoadTransformPreference(PrefPrefix + "RightPosteriorBone");
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
            PrefPrefix + "LeftPosteriorBone",
            leftPosteriorBone
        );

        SaveTransformPreference(
            PrefPrefix + "RightPosteriorBone",
            rightPosteriorBone
        );
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "SlimeVR Posterior Tracking Configurator",
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
            "Posterior Bones",
            EditorStyles.boldLabel
        );

        leftPosteriorBone =
            (Transform)EditorGUILayout.ObjectField(
                "Left Posterior",
                leftPosteriorBone,
                typeof(Transform),
                true
            );

        rightPosteriorBone =
            (Transform)EditorGUILayout.ObjectField(
                "Right Posterior",
                rightPosteriorBone,
                typeof(Transform),
                true
            );

        if (GUILayout.Button("Auto Fill Bones"))
        {
            AutoFillPosteriorBones();
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
            (leftPosteriorBone != null || rightPosteriorBone != null);

        if (GUILayout.Button("Generate Posterior Support"))
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

        if (leftPosteriorBone == null && rightPosteriorBone == null)
        {
            EditorUtility.DisplayDialog(
                "Missing Posterior Bones",
                "Assign at least one posterior bone.",
                "OK"
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(clipOutputFolder))
        {
            clipOutputFolder = "Assets/Animations/PosteriorTracking";
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
        if (leftPosteriorBone != null)
        {
            GeneratePosterior(
                "LeftPosterior",
                leftPosteriorBone,
                LeftPitch,
                LeftYaw
            );
        }

        if (rightPosteriorBone != null)
        {
            GeneratePosterior(
                "RightPosterior",
                rightPosteriorBone,
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
            "Posterior configuration completed!",
            "Posterior tracking support created successfully!",
            "OK"
        );
    }

    private void GeneratePosterior(
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
                "[PosteriorRig] The selected AnimatorController is not a saved asset."
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
            $"[PosteriorRig] {sideName}: RAW OSC Float -> ParameterDriver 4-bit synced Bool encoder; " +
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
        AddUnsyncedFloatParameter(PosteriorVertical);
        AddEncodedBoolParameters(PosteriorVertical);

        RemoveExistingLayer("PosteriorVertical");
        RemoveExistingLayer("PosteriorVertical_4BitEncode");
        RemoveExistingLayer("PosteriorVertical_4BitDecode");

        string controllerPath =
            AssetDatabase.GetAssetPath(targetController);

        if (string.IsNullOrEmpty(controllerPath))
        {
            Debug.LogError(
                "[PosteriorRig] The selected AnimatorController is not a saved asset."
            );
            return;
        }

        GenerateFourBitEncoderLayer(
            "PosteriorVertical_4BitEncode",
            PosteriorVertical,
            controllerPath
        );

        GenerateFourBitDecoderLayer(
            "PosteriorVertical_4BitDecode",
            PosteriorVertical,
            controllerPath
        );

        string resolvedVerticalParameter =
            useOSCSmoothPath
                ? "OSCm/Proxy/" + PosteriorVertical
                : PosteriorVertical;

        EnsureAnimatorFloatParameter(resolvedVerticalParameter);

        var stateMachine =
            new AnimatorStateMachine
            {
                name = "PosteriorVertical"
            };

        AssetDatabase.AddObjectToAsset(
            stateMachine,
            controllerPath
        );

        var verticalTree =
            new BlendTree
            {
                name = "PosteriorVertical_BlendTree",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false,
                blendParameter = resolvedVerticalParameter
            };

        AssetDatabase.AddObjectToAsset(
            verticalTree,
            controllerPath
        );

        AnimationClip verticalNegative =
            CreateWorldRelativeVerticalClip(
                "PosteriorVertical_Negative",
                -maxVertical
            );

        AnimationClip verticalNeutral =
            CreateWorldRelativeVerticalClip(
                "PosteriorVertical_Neutral",
                0f
            );

        AnimationClip verticalPositive =
            CreateWorldRelativeVerticalClip(
                "PosteriorVertical_Positive",
                maxVertical
            );

        verticalTree.AddChild(
            verticalNegative,
            -1f
        );

        verticalTree.AddChild(
            verticalNeutral,
            0f
        );

        verticalTree.AddChild(
            verticalPositive,
            1f
        );

        AnimatorState state =
            stateMachine.AddState("Tracking");

        state.motion = verticalTree;
        state.writeDefaultValues = true;

        stateMachine.defaultState = state;

        var layer =
            new AnimatorControllerLayer
            {
                name = "PosteriorVertical",
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

        var layers =
            targetController.layers.ToList();

        layers.Add(layer);

        targetController.layers =
            layers.ToArray();

        EditorUtility.SetDirty(verticalTree);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(targetController);

        Debug.Log(
            "[PosteriorRig] Shared PosteriorVertical generated: local Float encoder, " +
            "four synced bits, remote Float decoder, and one position-only motion layer."
        );
    }

    private AnimationClip CreateWorldRelativeVerticalClip(
        string name,
        float verticalMeters)
    {
        var clip =
            new AnimationClip
            {
                name = name,
                frameRate = 60f,
                wrapMode = WrapMode.Loop
            };

        if (leftPosteriorBone != null)
        {
            AddWorldRelativeVerticalCurves(
                clip,
                leftPosteriorBone,
                verticalMeters
            );
        }

        if (rightPosteriorBone != null)
        {
            AddWorldRelativeVerticalCurves(
                clip,
                rightPosteriorBone,
                verticalMeters
            );
        }

        string path =
            clipOutputFolder +
            "/" +
            name +
            ".anim";

        return SaveOrOverwriteClip(
            clip,
            path
        );
    }

    private void AddWorldRelativeVerticalCurves(
        AnimationClip clip,
        Transform bone,
        float verticalMeters)
    {
        string bonePath =
            GetBonePath(bone);

        if (string.IsNullOrEmpty(bonePath))
        {
            Debug.LogWarning(
                $"[PosteriorRig] Could not determine an animation path for '{bone.name}'."
            );
            return;
        }

        Transform avatarRoot =
            GetAvatarRoot(bone);

        Vector3 avatarUp =
            avatarRoot != null
                ? avatarRoot.up.normalized
                : Vector3.up;

        Vector3 originalWorldPosition =
            bone.position;

        Vector3 originalLocalPosition =
            bone.localPosition;

        Vector3 resultLocalPosition;

        try
        {
            bone.position =
                originalWorldPosition +
                avatarUp * verticalMeters;

            resultLocalPosition =
                bone.localPosition;
        } finally
        {
            bone.position =
                originalWorldPosition;
        }

        SetConstantCurve(
            clip,
            bonePath,
            "m_LocalPosition.x",
            resultLocalPosition.x
        );

        SetConstantCurve(
            clip,
            bonePath,
            "m_LocalPosition.y",
            resultLocalPosition.y
        );

        SetConstantCurve(
            clip,
            bonePath,
            "m_LocalPosition.z",
            resultLocalPosition.z
        );
        if (
            Mathf.Approximately(verticalMeters, 0f) &&
            (resultLocalPosition - originalLocalPosition).sqrMagnitude > 0.0000001f)
        {
            Debug.LogWarning(
                $"[PosteriorRig] Neutral vertical pose for '{bone.name}' did not match rest position."
            );
        }
    }

    private AnimationClip CreateWorldRelativeRotationClip(
        string name,
        Transform bone,
        float pitchDegrees,
        float yawDegrees)
    {
        var clip =
            new AnimationClip
            {
                name = name,
                frameRate = 60f,
                wrapMode = WrapMode.Loop
            };

        string bonePath =
            GetBonePath(bone);

        if (string.IsNullOrEmpty(bonePath))
        {
            Debug.LogWarning(
                $"[PosteriorRig] Could not determine an animation path for '{bone.name}'."
            );
        }

        Quaternion originalWorldRotation =
            bone.rotation;

        Vector3 originalLocalEuler =
            bone.localEulerAngles;

        Transform avatarRoot =
            GetAvatarRoot(bone);

        Vector3 avatarRight =
            avatarRoot != null
                ? avatarRoot.right.normalized
                : Vector3.right;

        Vector3 avatarUp =
            avatarRoot != null
                ? avatarRoot.up.normalized
                : Vector3.up;

        Vector3 resultLocalEuler;

        try
        {
            // Always begin from the avatar's authored/rest orientation.
            bone.rotation = originalWorldRotation;

            // Apply yaw around the avatar/world vertical axis.
            bone.Rotate(
                avatarUp,
                yawDegrees,
                Space.World
            );

            bone.Rotate(
                avatarRight,
                pitchDegrees,
                Space.World
            );

            resultLocalEuler =
                GetContinuousEuler(
                    originalLocalEuler,
                    bone.localEulerAngles
                );
        } finally
        {
            bone.rotation = originalWorldRotation;
        }

        SetConstantCurve(
            clip,
            bonePath,
            "localEulerAnglesRaw.x",
            resultLocalEuler.x
        );

        SetConstantCurve(
            clip,
            bonePath,
            "localEulerAnglesRaw.y",
            resultLocalEuler.y
        );

        SetConstantCurve(
            clip,
            bonePath,
            "localEulerAnglesRaw.z",
            resultLocalEuler.z
        );

        string path =
            clipOutputFolder +
            "/" +
            name +
            ".anim";

        return SaveOrOverwriteClip(
            clip,
            path
        );
    }

    private static Vector3 GetContinuousEuler(
        Vector3 reference,
        Vector3 euler)
    {
        return new Vector3(
            reference.x +
                Mathf.DeltaAngle(
                    reference.x,
                    euler.x
                ),

            reference.y +
                Mathf.DeltaAngle(
                    reference.y,
                    euler.y
                ),

            reference.z +
                Mathf.DeltaAngle(
                    reference.z,
                    euler.z
                )
        );
    }

    private static string MakeYawLabel(
        int index,
        int totalSteps,
        float normalizedYaw)
    {
        int middle =
            (totalSteps - 1) / 2;

        if (index == middle)
        {
            return "YawZero";
        }

        int distance =
            Mathf.Abs(index - middle);

        return normalizedYaw < 0f
            ? "YawN" + distance.ToString("00")
            : "YawP" + distance.ToString("00");
    }

    private static void SetConstantCurve(
        AnimationClip clip,
        string path,
        string property,
        float value)
    {
        clip.SetCurve(
            path,
            typeof(Transform),
            property,
            new AnimationCurve(
                new Keyframe(0f, value),
                new Keyframe(1f, value)
            )
        );
    }

    private void EnsureAnimatorBoolParameter(
        string parameter)
    {
        AnimatorControllerParameter[] matches =
            targetController.parameters
                .Where(p => p.name == parameter)
                .ToArray();

        if (
            matches.Length == 1 &&
            matches[0].type == AnimatorControllerParameterType.Bool)
        {
            return;
        }

        if (matches.Length > 0)
        {
            Debug.LogWarning(
                $"[PosteriorRig] Rebuilding Animator parameter '{parameter}' as Bool. " +
                $"Found {matches.Length} existing parameter(s)."
            );

            while (
                targetController.parameters.Any(
                    p => p.name == parameter
                ))
            {
                AnimatorControllerParameter existing =
                    targetController.parameters.First(
                        p => p.name == parameter
                    );

                targetController.RemoveParameter(existing);
            }
        }

        targetController.AddParameter(
            parameter,
            AnimatorControllerParameterType.Bool
        );

        EditorUtility.SetDirty(targetController);
    }

    private void EnsureAnimatorFloatParameter(
        string parameter)
    {
        AnimatorControllerParameter existing =
            targetController.parameters.FirstOrDefault(
                p => p.name == parameter
            );

        if (existing == null)
        {
            targetController.AddParameter(
                parameter,
                AnimatorControllerParameterType.Float
            );

            EditorUtility.SetDirty(targetController);
            return;
        }

        if (existing.type != AnimatorControllerParameterType.Float)
        {
            Debug.LogError(
                $"[PosteriorRig] Animator parameter '{parameter}' already exists " +
                $"but is {existing.type}, not Float."
            );
        }
    }

    private void AddUnsyncedFloatParameter(
        string parameter)
    {
        AnimatorControllerParameter existing =
            targetController.parameters.FirstOrDefault(
                p => p.name == parameter
            );

        if (existing == null)
        {
            targetController.AddParameter(
                parameter,
                AnimatorControllerParameterType.Float
            );
        } else if (
              existing.type !=
              AnimatorControllerParameterType.Float)
        {
            Debug.LogError(
                $"[PosteriorRig] Animator parameter '{parameter}' already exists " +
                $"but is {existing.type}, not Float."
            );
            return;
        }

        VRCExpressionParameters.Parameter expressionParameter =
            selectedExpParams.parameters?.FirstOrDefault(
                p => p.name == parameter
            );

        if (expressionParameter == null)
        {
            VRCExpressionUtility.AddMissingParameter(
                selectedExpParams,
                parameter,
                VRCExpressionParameters.ValueType.Float,
                false,
                0,
                false
            );

            expressionParameter =
                selectedExpParams.parameters?.FirstOrDefault(
                    p => p.name == parameter
                );

            if (expressionParameter != null)
            {
                expressionParameter.valueType =
                    VRCExpressionParameters.ValueType.Float;
                expressionParameter.networkSynced = false;
                expressionParameter.saved = false;
                EditorUtility.SetDirty(selectedExpParams);
            }
        } else
        {
            expressionParameter.valueType =
                VRCExpressionParameters.ValueType.Float;

            expressionParameter.networkSynced = false;
            expressionParameter.saved = false;

            EditorUtility.SetDirty(selectedExpParams);
        }
    }

    private void AddEncodedBoolParameters(
        string logicalParameter)
    {
        for (int bit = 0; bit < 4; bit++)
        {
            string bitParameter =
                GetBitParameterName(
                    logicalParameter,
                    bit
                );

            EnsureAnimatorBoolParameter(
                bitParameter
            );

            var expressionParameters =
                selectedExpParams.parameters?.ToList() ??
                new List<VRCExpressionParameters.Parameter>();

            int index =
                expressionParameters.FindIndex(
                    p => p.name == bitParameter
                );

            VRCExpressionParameters.Parameter parameter;

            if (index >= 0)
            {
                parameter =
                    expressionParameters[index];
            } else
            {
                parameter =
                    new VRCExpressionParameters.Parameter
                    {
                        name = bitParameter
                    };

                expressionParameters.Add(parameter);
                index = expressionParameters.Count - 1;
            }

            parameter.valueType =
                VRCExpressionParameters.ValueType.Bool;

            parameter.networkSynced = true;
            parameter.saved = false;
            parameter.defaultValue = 0f;

            expressionParameters[index] =
                parameter;

            selectedExpParams.parameters =
                expressionParameters.ToArray();
        }

        EditorUtility.SetDirty(targetController);
        EditorUtility.SetDirty(selectedExpParams);
    }

    private static string GetBitParameterName(
        string logicalParameter,
        int bit)
    {
        return logicalParameter + "_B" + bit;
    }

    private void GenerateFourBitEncoderLayer(
        string layerName,
        string rawSourceFloatParameter,
        string controllerPath)
    {

        if (
            rawSourceFloatParameter.StartsWith(
                "OSCm/Proxy/",
                System.StringComparison.Ordinal))
        {
            Debug.LogError(
                $"[PosteriorRig] Refusing to encode OSCmooth proxy '{rawSourceFloatParameter}'. " +
                "The encoder must read the raw logical OSC Float."
            );
            return;
        }

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
                new Vector3(0f, 0f, 0f)
            );

        idleState.writeDefaultValues = true;
        stateMachine.defaultState = idleState;

        for (
            int quantizedIndex = 0;
            quantizedIndex < EncodedLevels;
            quantizedIndex++)
        {
            int wireCode =
                QuantizedIndexToWireCode(
                    quantizedIndex
                );

            AnimatorState state =
                stateMachine.AddState(
                    $"Q{quantizedIndex:00}_Code{wireCode:X1}",
                    new Vector3(
                        280f * (quantizedIndex % 4),
                        100f + 90f * (quantizedIndex / 4),
                        0f
                    )
                );

            state.writeDefaultValues = true;

            var driver =
                state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

            driver.localOnly = false;
            driver.parameters =
                new List<VRC_AvatarParameterDriver.Parameter>();

            for (int bit = 0; bit < 4; bit++)
            {
                bool bitSet =
                    (wireCode & (1 << bit)) != 0;

                driver.parameters.Add(
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        name =
                            GetBitParameterName(
                                rawSourceFloatParameter,
                                bit
                            ),
                        type =
                            VRC_AvatarParameterDriver.ChangeType.Set,
                        value = bitSet ? 1f : 0f
                    }
                );
            }

            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(state);

            AnimatorStateTransition transition =
                stateMachine.AddAnyStateTransition(
                    state
                );

            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;

            AddForcedBoolCondition(
                transition,
                "IsLocal",
                true
            );

            if (quantizedIndex > 0)
            {
                float lowerBoundary =
                    -1f +
                    (quantizedIndex - 0.5f) /
                    EncodedMiddle;

                transition.AddCondition(
                    AnimatorConditionMode.Greater,
                    lowerBoundary,
                    rawSourceFloatParameter
                );
            }

            if (quantizedIndex < EncodedLevels - 1)
            {
                float upperBoundary =
                    -1f +
                    (quantizedIndex + 0.5f) /
                    EncodedMiddle;

                transition.AddCondition(
                    AnimatorConditionMode.Less,
                    upperBoundary,
                    rawSourceFloatParameter
                );
            }

            EditorUtility.SetDirty(transition);
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

        EditorUtility.SetDirty(idleState);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(targetController);

        Debug.Log(
            $"[PosteriorRig] Generated ParameterDriver encoder '{layerName}' from raw Float " +
            $"'{rawSourceFloatParameter}' into four synced Bool parameters."
        );
    }

    private AnimationClip CreateFourBitAAPEncodeClip(
        string logicalParameter,
        int quantizedIndex,
        int wireCode)
    {
        string safeParameterName =
            logicalParameter.Replace("/", "_");

        string clipName =
            safeParameterName +
            "_AAPEncode_Q" +
            quantizedIndex.ToString("00") +
            "_Code" +
            wireCode.ToString("X1");

        var clip =
            new AnimationClip
            {
                name = clipName,
                frameRate = 60f,
                wrapMode = WrapMode.Loop
            };

        for (int bit = 0; bit < 4; bit++)
        {
            string bitParameter =
                GetBitParameterName(
                    logicalParameter,
                    bit
                );

            float bitValue =
                (wireCode & (1 << bit)) != 0
                    ? 1f
                    : 0f;

            EditorCurveBinding binding =
                EditorCurveBinding.FloatCurve(
                    "",
                    typeof(Animator),
                    bitParameter
                );

            AnimationCurve curve =
                AnimationCurve.Constant(
                    0f,
                    1f / 60f,
                    bitValue
                );

            AnimationUtility.SetEditorCurve(
                clip,
                binding,
                curve
            );
        }

        string path =
            clipOutputFolder +
            "/" +
            clipName +
            ".anim";

        return SaveOrOverwriteClip(
            clip,
            path
        );
    }

    private static void AddForcedBoolCondition(
        AnimatorStateTransition transition,
        string parameterName,
        bool expectedValue)
    {
        AnimatorCondition[] retainedConditions =
            transition.conditions
                .Where(c => c.parameter != parameterName)
                .ToArray();

        transition.conditions = retainedConditions;

        transition.AddCondition(
            expectedValue
                ? AnimatorConditionMode.If
                : AnimatorConditionMode.IfNot,
            0f,
            parameterName
        );

        EditorUtility.SetDirty(transition);
    }

    private void GenerateFourBitDecoderLayer(
        string layerName,
        string decodedFloatParameter,
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

        AnimatorState idleState =
            stateMachine.AddState(
                "Local_Idle",
                new Vector3(0f, 0f, 0f)
            );

        idleState.writeDefaultValues = true;
        stateMachine.defaultState = idleState;

        for (int wireCode = 0; wireCode < 16; wireCode++)
        {
            int quantizedIndex =
                WireCodeToQuantizedIndex(
                    wireCode
                );

            bool valid =
                quantizedIndex >= 0 &&
                quantizedIndex < EncodedLevels;

            float decodedValue =
                valid
                    ? (quantizedIndex - EncodedMiddle) /
                        (float)EncodedMiddle
                    : 0f;

            string stateName =
                valid
                    ? $"Code_{wireCode:X1}_Step_{quantizedIndex:00}"
                    : $"Code_{wireCode:X1}_FallbackNeutral";

            AnimatorState state =
                stateMachine.AddState(
                    stateName,
                    new Vector3(
                        260f * (wireCode % 4),
                        100f + 90f * (wireCode / 4),
                        0f
                    )
                );

            state.writeDefaultValues = true;

            var driver =
                state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

            driver.localOnly = false;
            driver.parameters =
                new List<VRC_AvatarParameterDriver.Parameter>
                {
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        name = decodedFloatParameter,
                        type =
                            VRC_AvatarParameterDriver.ChangeType.Set,
                        value = decodedValue
                    }
                };

            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(state);

            AnimatorStateTransition transition =
                stateMachine.AddAnyStateTransition(
                    state
                );

            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;

            AddForcedBoolCondition(
                transition,
                "IsLocal",
                false
            );

            for (int bit = 0; bit < 4; bit++)
            {
                bool bitSet =
                    (wireCode & (1 << bit)) != 0;

                transition.AddCondition(
                    bitSet
                        ? AnimatorConditionMode.If
                        : AnimatorConditionMode.IfNot,
                    0f,
                    GetBitParameterName(
                        decodedFloatParameter,
                        bit
                    )
                );
            }

            EditorUtility.SetDirty(transition);
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

        EditorUtility.SetDirty(idleState);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(targetController);

        Debug.Log(
            $"[PosteriorRig] Generated Bool-state ParameterDriver decoder '{layerName}' into raw Float " +
            $"'{decodedFloatParameter}'."
        );
    }

    private BlendTree CreateAAPFourBitDecoderTree(
        string decodedFloatParameter,
        string controllerPath)
    {
        return CreateAAPDecoderBitTree(
            decodedFloatParameter,
            controllerPath,
            bitIndex: 3,
            wireCodePrefix: 0
        );
    }

    private BlendTree CreateAAPDecoderBitTree(
        string decodedFloatParameter,
        string controllerPath,
        int bitIndex,
        int wireCodePrefix)
    {
        string bitParameter =
            GetBitParameterName(
                decodedFloatParameter,
                bitIndex
            );

        var tree =
            new BlendTree
            {
                name =
                    decodedFloatParameter +
                    "_AAPDecode_B" +
                    bitIndex +
                    "_Prefix" +
                    wireCodePrefix.ToString("X1"),
                blendType = BlendTreeType.Simple1D,
                blendParameter = bitParameter,
                useAutomaticThresholds = false
            };

        AssetDatabase.AddObjectToAsset(
            tree,
            controllerPath
        );

        for (int bitValue = 0; bitValue <= 1; bitValue++)
        {
            int wireCode =
                wireCodePrefix |
                (bitValue << bitIndex);

            Motion childMotion;

            if (bitIndex == 0)
            {
                int quantizedIndex =
                    WireCodeToQuantizedIndex(
                        wireCode
                    );

                bool valid =
                    quantizedIndex >= 0 &&
                    quantizedIndex < EncodedLevels;

                float decodedValue =
                    valid
                        ? (quantizedIndex - EncodedMiddle) /
                            (float)EncodedMiddle
                        : 0f;

                childMotion =
                    CreateFourBitAAPDecodeClip(
                        decodedFloatParameter,
                        wireCode,
                        decodedValue
                    );
            } else
            {
                childMotion =
                    CreateAAPDecoderBitTree(
                        decodedFloatParameter,
                        controllerPath,
                        bitIndex - 1,
                        wireCode
                    );
            }

            tree.AddChild(
                childMotion,
                bitValue
            );
        }

        EditorUtility.SetDirty(tree);
        return tree;
    }

    private AnimationClip CreateFourBitAAPDecodeClip(
        string logicalParameter,
        int wireCode,
        float decodedValue)
    {
        string safeParameterName =
            logicalParameter.Replace("/", "_");

        string clipName =
            safeParameterName +
            "_AAPDecode_Code" +
            wireCode.ToString("X1");

        var clip =
            new AnimationClip
            {
                name = clipName,
                frameRate = 60f,
                wrapMode = WrapMode.Loop
            };

        EditorCurveBinding binding =
            EditorCurveBinding.FloatCurve(
                "",
                typeof(Animator),
                logicalParameter
            );

        AnimationCurve curve =
            AnimationCurve.Constant(
                0f,
                1f / 60f,
                decodedValue
            );

        AnimationUtility.SetEditorCurve(
            clip,
            binding,
            curve
        );

        string path =
            clipOutputFolder +
            "/" +
            clipName +
            ".anim";

        return SaveOrOverwriteClip(
            clip,
            path
        );
    }
    private static int QuantizedIndexToWireCode(
        int quantizedIndex)
    {
        quantizedIndex =
            Mathf.Clamp(
                quantizedIndex,
                0,
                EncodedLevels - 1
            );

        int gray =
            quantizedIndex ^
            (quantizedIndex >> 1);

        return gray ^ NeutralGrayCode;
    }

    private static int WireCodeToQuantizedIndex(
        int wireCode)
    {
        int gray =
            wireCode ^
            NeutralGrayCode;

        int binary = 0;

        for (
            int value = gray;
            value != 0;
            value >>= 1)
        {
            binary ^= value;
        }

        return binary;
    }

    private void RemoveExistingLayer(
        string layerName)
    {
        string controllerPath =
            AssetDatabase.GetAssetPath(targetController);

        var layers =
            targetController.layers.ToList();

        int index =
            layers.FindIndex(
                l => l.name == layerName
            );

        if (index < 0)
        {
            return;
        }

        AnimatorControllerLayer existing =
            layers[index];

        layers.RemoveAt(index);

        targetController.layers =
            layers.ToArray();

        if (existing.stateMachine != null)
        {
            DestroyStateMachineRecursive(
                existing.stateMachine
            );
        }

        EditorUtility.SetDirty(targetController);

        if (!string.IsNullOrEmpty(controllerPath))
        {
            AssetDatabase.ImportAsset(
                controllerPath,
                ImportAssetOptions.ForceUpdate
            );
        }
    }

    private static void DestroyStateMachineRecursive(
        AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null)
        {
            return;
        }

        foreach (
            ChildAnimatorStateMachine childStateMachine
            in stateMachine.stateMachines)
        {
            DestroyStateMachineRecursive(
                childStateMachine.stateMachine
            );
        }

        foreach (
            ChildAnimatorState child
            in stateMachine.states)
        {
            AnimatorState state =
                child.state;

            if (state == null)
            {
                continue;
            }

            DestroyMotionRecursive(
                state.motion
            );

            foreach (
                AnimatorStateTransition transition
                in state.transitions)
            {
                if (transition != null)
                {
                    Object.DestroyImmediate(
                        transition,
                        true
                    );
                }
            }

            Object.DestroyImmediate(
                state,
                true
            );
        }

        Object.DestroyImmediate(
            stateMachine,
            true
        );
    }

    private static void DestroyMotionRecursive(
        Motion motion)
    {
        BlendTree tree =
            motion as BlendTree;

        if (tree == null)
        {
            return;
        }

        ChildMotion[] children =
            tree.children;

        foreach (
            ChildMotion child
            in children)
        {
            if (child.motion is BlendTree)
            {
                DestroyMotionRecursive(
                    child.motion
                );
            }
        }

        Object.DestroyImmediate(
            tree,
            true
        );
    }

    private void AutoFillPosteriorBones()
    {
        Animator[] animators =
            FindObjectsOfType<Animator>();

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
            animators.FirstOrDefault(
                a => a.isHuman
            ) ??
            animators[0];

        Transform avatarRoot =
            avatarAnimator.transform;

        Transform[] transforms =
            avatarRoot.GetComponentsInChildren<Transform>(
                true
            );

        foreach (
            Transform t
            in transforms)
        {
            string normalized =
                t.name
                    .ToLowerInvariant()
                    .Replace(" ", "")
                    .Replace("_", "")
                    .Replace("-", "")
                    .Replace(".", "");

            bool posteriorLike =
                normalized.Contains("posterior") ||
                normalized.Contains("hips");

            if (!posteriorLike)
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

            if (
                left &&
                leftPosteriorBone == null)
            {
                leftPosteriorBone = t;

                Debug.Log(
                    $"[PosteriorRig] Auto-mapped {t.name} -> Left Posterior"
                );
            } else if (
                  right &&
                  rightPosteriorBone == null)
            {
                rightPosteriorBone = t;

                Debug.Log(
                    $"[PosteriorRig] Auto-mapped {t.name} -> Right Posterior"
                );
            }
        }

        Repaint();
    }

    private string GetBonePath(
        Transform bone)
    {
        if (bone == null)
        {
            return "";
        }
        string path =
            bone.name;

        Transform parent =
            bone.parent;

        while (parent != null)
        {
            path =
                parent.name +
                "/" +
                path;

            if (
                parent.name.StartsWith(
                    "Armature"
                ))
            {
                break;
            }

            parent =
                parent.parent;
        }

        return path;
    }

    private Transform GetAvatarRoot(
        Transform transform)
    {
        if (transform == null)
        {
            return null;
        }

        Transform current =
            transform;

        while (current != null)
        {
            Animator animator =
                current.GetComponent<Animator>();

            if (animator != null)
            {
                return current;
            }

            current =
                current.parent;
        }

        return transform.root;
    }

    private AnimationClip SaveOrOverwriteClip(
        AnimationClip clip,
        string fullPath)
    {
        string directory =
            Path.GetDirectoryName(fullPath)
                ?.Replace("\\", "/");

        if (string.IsNullOrEmpty(directory))
        {
            Debug.LogError(
                $"[PosteriorRig] Invalid animation path: {fullPath}"
            );
            return null;
        }

        EnsureFolder(directory);

        AnimationClip existing =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(
                fullPath
            );

        if (existing != null)
        {
            // Preserve the asset GUID so references stay stable when regenerating.
            EditorUtility.CopySerialized(
                clip,
                existing
            );

            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(clip);
        } else
        {
            AssetDatabase.CreateAsset(
                clip,
                fullPath
            );
        }

        AssetDatabase.ImportAsset(
            fullPath,
            ImportAssetOptions.ForceUpdate
        );
        return existing != null
            ? existing
            : AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
    }

    private void EnsureFolder(
        string folder)
    {
        folder =
            folder
                .Replace("\\", "/")
                .TrimEnd('/');

        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        if (!folder.StartsWith("Assets"))
        {
            Debug.LogError(
                $"[PosteriorRig] Folder must be inside Assets: {folder}"
            );
            return;
        }

        string[] parts =
            folder.Split('/');

        string current =
            parts[0];

        for (
            int i = 1;
            i < parts.Length;
            i++)
        {
            string next =
                current +
                "/" +
                parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]
                );
            }

            current =
                next;
        }
    }

    private static void SaveAssetPreference<T>(
        string key,
        T asset)
        where T : Object
    {
        if (asset == null)
        {
            EditorPrefs.DeleteKey(key);
            return;
        }

        string path =
            AssetDatabase.GetAssetPath(asset);

        if (string.IsNullOrEmpty(path))
        {
            EditorPrefs.DeleteKey(key);
            return;
        }

        EditorPrefs.SetString(
            key,
            path
        );
    }

    private static T LoadAssetPreference<T>(
        string key)
        where T : Object
    {
        string path =
            EditorPrefs.GetString(
                key,
                ""
            );

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<T>(
            path
        );
    }

    private static void SaveTransformPreference(
        string key,
        Transform transform)
    {
        if (transform == null)
        {
            EditorPrefs.DeleteKey(key);
            return;
        }

        GlobalObjectId id =
            GlobalObjectId.GetGlobalObjectIdSlow(
                transform
            );

        EditorPrefs.SetString(
            key,
            id.ToString()
        );
    }

    private static Transform LoadTransformPreference(
        string key)
    {
        string serializedId =
            EditorPrefs.GetString(
                key,
                ""
            );

        if (string.IsNullOrEmpty(serializedId))
        {
            return null;
        }

        if (!GlobalObjectId.TryParse(
                serializedId,
                out GlobalObjectId id))
        {
            return null;
        }

        return
            GlobalObjectId
                .GlobalObjectIdentifierToObjectSlow(id)
            as Transform;
    }
}
