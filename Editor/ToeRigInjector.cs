using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

public class ToeRigInjector : EditorWindow
{

    private AnimatorController targetController;
    private VRCExpressionParameters selectedExpParams;

    private string clipOutputFolder = "Assets/Animations/ToeBlendAnimations";
    private Vector2 scroll;

    private readonly Transform[] leftFootBones = new Transform[5];
    private readonly Transform[] rightFootBones = new Transform[5];

    private readonly float[] leftFootSplay =
        new float[5] { 90f, -15f, -30f, -60f, -90f };

    private readonly float[] rightFootSplay =
        new float[5] { -90f, 15f, 30f, 60f, 90f };

    private float curlMinX = 90f;
    private float curlMaxX = -90f;

    private bool useOSCSmoothPath;
    private bool invertValues;

    private const int SplaySteps = 15;

    private const string NoToesParameter = "NoToes";

    private static readonly string[] LeftCurlParameters =
    {
        "ToeLeft1Float",
        "ToeLeft2Float",
        "ToeLeft2Float",
        "ToeLeft5Float",
        "ToeLeft5Float"
    };

    private static readonly string[] RightCurlParameters =
    {
        "ToeRight1Float",
        "ToeRight2Float",
        "ToeRight2Float",
        "ToeRight5Float",
        "ToeRight5Float"
    };

    private static readonly string[] LeftSplayParameters =
    {
        "ToeSplayLeft1Float",
        "ToeSplayLeft5Float",
        "ToeSplayLeft5Float",
        "ToeSplayLeft5Float",
        "ToeSplayLeft5Float"
    };

    private static readonly string[] RightSplayParameters =
    {
        "ToeSplayRight1Float",
        "ToeSplayRight5Float",
        "ToeSplayRight5Float",
        "ToeSplayRight5Float",
        "ToeSplayRight5Float"
    };

    [MenuItem("Tools/Toe Rig/Add Toe Tracking Compatibility")]
    public static void Open()
    {
        GetWindow<ToeRigInjector>(
            "Toe Tracking Configurator"
        );
    }

    private void OnEnable()
    {
        targetController =
            Prefs.GetObject<AnimatorController>(
                "ToeRig_TargetController_" +
                EditorSceneManager.GetActiveScene().name
            );

        LoadConfig();
    }

    private void LoadConfig()
    {
        if (targetController == null)
        {
            return;
        }

        selectedExpParams =
            Prefs.GetObject<VRCExpressionParameters>(
                "ToeRig_SelectedExpParams_" +
                targetController.name
            );

        useOSCSmoothPath =
            EditorPrefs.GetBool(
                "ToeRig_OSCPath_" +
                targetController.name,
                false
            );

        invertValues =
            EditorPrefs.GetBool(
                "ToeRig_InvertValuePath_" +
                targetController.name,
                false
            );

        for (int i = 0; i < 5; i++)
        {
            leftFootSplay[i] =
                EditorPrefs.GetFloat(
                    $"ToeRig_LeftSplay_{i}_" +
                    targetController.name,
                    leftFootSplay[i]
                );

            rightFootSplay[i] =
                EditorPrefs.GetFloat(
                    $"ToeRig_RightSplay_{i}_" +
                    targetController.name,
                    rightFootSplay[i]
                );

            leftFootBones[i] =
                BonePrefs.LoadBone(
                    $"ToeRig_Left_{i}_" +
                    targetController.name
                );

            rightFootBones[i] =
                BonePrefs.LoadBone(
                    $"ToeRig_Right_{i}_" +
                    targetController.name
                );
        }
    }

    private void OnDisable()
    {
        if (targetController == null)
        {
            return;
        }

        Prefs.SetObject(
            "ToeRig_TargetController_" +
            EditorSceneManager.GetActiveScene().name,
            targetController
        );

        Prefs.SetObject(
            "ToeRig_SelectedExpParams_" +
            targetController.name,
            selectedExpParams
        );

        EditorPrefs.SetBool(
            "ToeRig_OSCPath_" +
            targetController.name,
            useOSCSmoothPath
        );

        EditorPrefs.SetBool(
            "ToeRig_InvertValuePath_" +
            targetController.name,
            invertValues
        );

        for (int i = 0; i < 5; i++)
        {
            EditorPrefs.SetFloat(
                $"ToeRig_LeftSplay_{i}_" +
                targetController.name,
                leftFootSplay[i]
            );

            EditorPrefs.SetFloat(
                $"ToeRig_RightSplay_{i}_" +
                targetController.name,
                rightFootSplay[i]
            );

            BonePrefs.SaveBone(
                $"ToeRig_Left_{i}_" +
                targetController.name,
                leftFootBones[i]
            );

            BonePrefs.SaveBone(
                $"ToeRig_Right_{i}_" +
                targetController.name,
                rightFootBones[i]
            );
        }
    }

    private void OnGUI()
    {
        scroll =
            EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "Toe Tracking Compatibility Configurator",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space();

        selectedExpParams =
            (VRCExpressionParameters)
            EditorGUILayout.ObjectField(
                "VRC Expression Parameters",
                selectedExpParams,
                typeof(VRCExpressionParameters),
                false
            );

        AnimatorController newController =
            (AnimatorController)
            EditorGUILayout.ObjectField(
                "Target Animator Controller",
                targetController,
                typeof(AnimatorController),
                false
            );

        if (newController != targetController)
        {
            targetController = newController;
            LoadConfig();
        }

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

        invertValues =
            EditorGUILayout.Toggle(
                "Invert Values",
                invertValues
            );

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Left Foot Maximum Splay (big-to-pinky)"
        );

        for (int i = 0; i < 5; i++)
        {
            leftFootSplay[i] =
                EditorGUILayout.FloatField(
                    $"Toe {i + 1}",
                    leftFootSplay[i]
                );
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Right Foot Maximum Splay (big-to-pinky)"
        );

        for (int i = 0; i < 5; i++)
        {
            rightFootSplay[i] =
                EditorGUILayout.FloatField(
                    $"Toe {i + 1}",
                    rightFootSplay[i]
                );
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Toe Bone Assignment",
            EditorStyles.boldLabel
        );

        if (GUILayout.Button("Auto Fill Bones"))
        {
            AutoFillBones();
        }

        string[] toeLabels =
        {
            "Big Toe",
            "Index Toe",
            "Middle Toe",
            "Ring Toe",
            "Little Toe"
        };

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Left Foot Toe Bones");

        for (int i = 0; i < 5; i++)
        {
            leftFootBones[i] =
                (Transform)
                EditorGUILayout.ObjectField(
                    toeLabels[i],
                    leftFootBones[i],
                    typeof(Transform),
                    true
                );
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Right Foot Toe Bones");

        for (int i = 0; i < 5; i++)
        {
            rightFootBones[i] =
                (Transform)
                EditorGUILayout.ObjectField(
                    toeLabels[i],
                    rightFootBones[i],
                    typeof(Transform),
                    true
                );
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Toe Support"))
        {
            if (
                targetController == null ||
                selectedExpParams == null)
            {
                EditorUtility.DisplayDialog(
                    "Missing data",
                    "Please assign both the AnimatorController and VRC Expression Parameters.",
                    "OK"
                );
            } else
            {
                ApplyInjection();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void ApplyInjection()
    {
        EnsureFolder(clipOutputFolder);

        EnsureAnimatorParameter(
            NoToesParameter,
            AnimatorControllerParameterType.Bool
        );

        EnsureExpressionBoolParameter(
            NoToesParameter,
            true
        );

        Install(
            targetController,
            selectedExpParams
        );

        string controllerPath =
            AssetDatabase.GetAssetPath(
                targetController
            );

        if (string.IsNullOrEmpty(controllerPath))
        {
            Debug.LogError(
                "[ToeRig] Target controller is not a saved asset."
            );

            return;
        }

        var layers =
            targetController.layers.ToList();

        for (int toeIndex = 0; toeIndex < 5; toeIndex++)
        {
            GenerateSideToeIfAssigned(
                true,
                toeIndex,
                leftFootBones[toeIndex],
                LeftCurlParameters[toeIndex],
                LeftSplayParameters[toeIndex],
                controllerPath,
                layers
            );

            GenerateSideToeIfAssigned(
                false,
                toeIndex,
                rightFootBones[toeIndex],
                RightCurlParameters[toeIndex],
                RightSplayParameters[toeIndex],
                controllerPath,
                layers
            );
        }

        targetController.layers =
            layers.ToArray();

        EditorUtility.SetDirty(
            targetController
        );

        EditorUtility.SetDirty(
            selectedExpParams
        );

        AssetDatabase.SaveAssets();

        AssetDatabase.ImportAsset(
            controllerPath,
            ImportAssetOptions.ForceUpdate
        );

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Toe configuration completed!",
            "Toe support has been fully integrated!",
            "OK"
        );
    }

    private void GenerateSideToeIfAssigned(
        bool isLeftFoot,
        int toeIndex,
        Transform toeBone,
        string curlParameter,
        string splayParameter,
        string controllerPath,
        List<AnimatorControllerLayer> layers)
    {
        string sideName =
            isLeftFoot
                ? "Left"
                : "Right";

        string layerName =
            $"Toe{sideName}{toeIndex + 1}";

        AnimatorControllerLayer oldLayer =
            layers.FirstOrDefault(
                l =>
                    l.name ==
                    layerName
            );

        if (oldLayer != null)
        {
            layers.Remove(
                oldLayer
            );

            if (oldLayer.stateMachine != null)
            {
                DestroyStateMachineRecursive(
                    oldLayer.stateMachine
                );
            }
        }

        if (toeBone == null)
        {
            return;
        }

        EnsureRawUnsyncedFloat(
            curlParameter
        );

        EnsureRawUnsyncedFloat(
            splayParameter
        );

        string resolvedCurlParameter =
            GetMotionParameter(
                curlParameter,
                useOSCSmoothPath
            );

        string resolvedSplayParameter =
            GetMotionParameter(
                splayParameter,
                useOSCSmoothPath
            );

        EnsureAnimatorParameter(
            resolvedCurlParameter,
            AnimatorControllerParameterType.Float
        );

        EnsureAnimatorParameter(
            resolvedSplayParameter,
            AnimatorControllerParameterType.Float
        );

        AnimatorControllerLayer newLayer =
            GenerateToeMotionLayer(
                layerName,
                toeBone,
                isLeftFoot,
                toeIndex,
                resolvedCurlParameter,
                resolvedSplayParameter,
                controllerPath
            );

        layers.Add(
            newLayer
        );
    }

    private AnimatorControllerLayer GenerateToeMotionLayer(
        string layerName,
        Transform toeBone,
        bool isLeftFoot,
        int toeIndex,
        string curlParameter,
        string splayParameter,
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

        AnimatorState tracking =
            stateMachine.AddState(
                "Tracking"
            );

        tracking.writeDefaultValues = true;

        BlendTree splayTree =
            GenerateNestedSplayCurlTree(
                layerName,
                toeBone,
                isLeftFoot,
                toeIndex,
                curlParameter,
                splayParameter,
                controllerPath
            );

        tracking.motion =
            splayTree;

        AnimatorState noToes =
            stateMachine.AddState(
                "NoToes"
            );

        noToes.writeDefaultValues = true;

        AnimationClip neutral =
            CreateCombinedToeClip(
                layerName +
                "_DisabledNeutral",
                toeBone,
                0f,
                0f
            );

        SaveGeneratedClip(
            neutral,
            layerName +
            "_DisabledNeutral"
        );

        noToes.motion =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(
                GetClipPath(
                    layerName +
                    "_DisabledNeutral"
                )
            );

        stateMachine.defaultState =
            tracking;

        EnsureAnimatorParameter(
            "NoToes",
            AnimatorControllerParameterType.Bool
        );

        AnimatorStateTransition disable =
            tracking.AddTransition(
                noToes
            );

        disable.hasExitTime = false;
        disable.duration = 0.25f;
        disable.hasFixedDuration = true;

        disable.AddCondition(
            AnimatorConditionMode.If,
            0f,
            NoToesParameter
        );

        AnimatorStateTransition enable =
            noToes.AddTransition(
                tracking
            );

        enable.hasExitTime = false;
        enable.duration = 0.25f;
        enable.hasFixedDuration = true;

        enable.AddCondition(
            AnimatorConditionMode.IfNot,
            0f,
            NoToesParameter
        );

        EditorUtility.SetDirty(
            disable
        );

        EditorUtility.SetDirty(
            enable
        );

        EditorUtility.SetDirty(
            tracking
        );

        EditorUtility.SetDirty(
            noToes
        );

        EditorUtility.SetDirty(
            stateMachine
        );

        return new AnimatorControllerLayer
        {
            name = layerName,
            defaultWeight = 1f,
            stateMachine = stateMachine
        };
    }

    private BlendTree GenerateNestedSplayCurlTree(
        string layerName,
        Transform toeBone,
        bool isLeftFoot,
        int toeIndex,
        string curlParameter,
        string splayParameter,
        string controllerPath)
    {
        var outer =
            new BlendTree
            {
                name =
                    layerName +
                    "_SplayCurl",
                blendType =
                    BlendTreeType.Simple1D,
                useAutomaticThresholds =
                    false,
                blendParameter =
                    splayParameter
            };

        AssetDatabase.AddObjectToAsset(
            outer,
            controllerPath
        );

        float maximumSplay =
            Mathf.Abs(
                GetToeSplay(
                    isLeftFoot,
                    toeIndex
                )
            );

        for (
            int splayIndex = 0;
            splayIndex < SplaySteps;
            splayIndex++)
        {
            float normalizedSplay =
                Mathf.Lerp(
                    -1f,
                    1f,
                    splayIndex /
                    (float)(SplaySteps - 1)
                );

            float splayDegrees =
                -normalizedSplay *
                maximumSplay;

            if (invertValues)
            {
                splayDegrees =
                    -splayDegrees;
            }

            var curlTree =
                new BlendTree
                {
                    name =
                        layerName +
                        $"_Splay_{splayIndex:00}_Curl",
                    blendType =
                        BlendTreeType.Simple1D,
                    useAutomaticThresholds =
                        false,
                    blendParameter =
                        curlParameter
                };

            AssetDatabase.AddObjectToAsset(
                curlTree,
                controllerPath
            );

            string prefix =
                layerName +
                $"_Splay_{splayIndex:00}";

            AnimationClip bent =
                CreateCombinedToeClip(
                    prefix +
                    "_Bent",
                    toeBone,
                    -1f,
                    splayDegrees
                );

            AnimationClip neutral =
                CreateCombinedToeClip(
                    prefix +
                    "_Neutral",
                    toeBone,
                    0f,
                    splayDegrees
                );

            AnimationClip tip =
                CreateCombinedToeClip(
                    prefix +
                    "_Tip",
                    toeBone,
                    1f,
                    splayDegrees
                );

            SaveGeneratedClip(
                bent,
                prefix +
                "_Bent"
            );

            SaveGeneratedClip(
                neutral,
                prefix +
                "_Neutral"
            );

            SaveGeneratedClip(
                tip,
                prefix +
                "_Tip"
            );

            curlTree.AddChild(
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    GetClipPath(
                        prefix +
                        "_Bent"
                    )
                ),
                -1f
            );

            curlTree.AddChild(
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    GetClipPath(
                        prefix +
                        "_Neutral"
                    )
                ),
                0f
            );

            curlTree.AddChild(
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    GetClipPath(
                        prefix +
                        "_Tip"
                    )
                ),
                1f
            );

            outer.AddChild(
                curlTree,
                normalizedSplay
            );

            EditorUtility.SetDirty(
                curlTree
            );
        }

        EditorUtility.SetDirty(
            outer
        );

        return outer;
    }

    private AnimationClip CreateCombinedToeClip(
        string clipName,
        Transform toeBone,
        float normalizedCurl,
        float splayDegrees)
    {
        var clip =
            new AnimationClip
            {
                name = clipName,
                frameRate = 60f,
                wrapMode = WrapMode.Loop
            };

        List<Transform> toeTransforms =
            new List<Transform>
            {
                toeBone
            };

        if (toeBone.childCount > 0)
        {
            Transform child =
                toeBone.GetChild(0);

            if (
                !child.name.EndsWith(
                    "_end",
                    StringComparison.OrdinalIgnoreCase))
            {
                toeTransforms.Add(child);
            }
        }

        Transform avatarRoot =
            GetAvatarRoot(toeBone);

        Vector3 curlAxis =
            avatarRoot != null
                ? avatarRoot.right
                : Vector3.right;

        Vector3 splayAxis =
            avatarRoot != null
                ? avatarRoot.up
                : Vector3.up;

        for (
            int i = 0;
            i < toeTransforms.Count;
            i++)
        {
            Transform segment =
                toeTransforms[i];

            Vector3 originalEuler =
                segment.localEulerAngles;

            Quaternion originalRotation =
                segment.rotation;

            float curlAngle;

            if (normalizedCurl < 0f)
            {
                curlAngle =
                    Mathf.Abs(normalizedCurl) *
                    (invertValues
                        ? -curlMinX
                        : curlMinX) /
                    toeTransforms.Count;
            } else
            {
                curlAngle =
                    i == 0
                        ? normalizedCurl *
                          (invertValues
                              ? -curlMaxX
                              : curlMaxX)
                        : 0f;
            }

            float segmentSplay =
                i == 0
                    ? splayDegrees
                    : 0f;

            segment.Rotate(
                curlAxis,
                curlAngle,
                Space.World
            );

            segment.Rotate(
                splayAxis,
                segmentSplay,
                Space.World
            );

            Vector3 resultEuler =
                GetContinuousEuler(
                    originalEuler,
                    segment.localEulerAngles
                );

            segment.rotation =
                originalRotation;

            SetConstantEulerCurves(
                clip,
                GetBonePath(segment),
                resultEuler
            );
        }

        return clip;
    }

    private void SetConstantEulerCurves(
        AnimationClip clip,
        string path,
        Vector3 euler)
    {
        clip.SetCurve(
            path,
            typeof(Transform),
            "localEulerAnglesRaw.x",
            ConstantCurve(euler.x)
        );

        clip.SetCurve(
            path,
            typeof(Transform),
            "localEulerAnglesRaw.y",
            ConstantCurve(euler.y)
        );

        clip.SetCurve(
            path,
            typeof(Transform),
            "localEulerAnglesRaw.z",
            ConstantCurve(euler.z)
        );
    }

    private static AnimationCurve ConstantCurve(
        float value)
    {
        return new AnimationCurve(
            new Keyframe(0f, value),
            new Keyframe(1f, value)
        );
    }



    private void EnsureRawUnsyncedFloat(
        string parameter)
    {
        EnsureAnimatorParameter(
            parameter,
            AnimatorControllerParameterType.Float
        );

        var parameters =
            selectedExpParams.parameters?.ToList() ??
            new List<VRCExpressionParameters.Parameter>();

        int index =
            parameters.FindIndex(
                p =>
                    p.name ==
                    parameter
            );

        VRCExpressionParameters.Parameter exp;

        if (index >= 0)
        {
            exp =
                parameters[index];
        } else
        {
            exp =
                new VRCExpressionParameters.Parameter
                {
                    name = parameter
                };

            parameters.Add(exp);
            index =
                parameters.Count - 1;
        }

        exp.valueType =
            VRCExpressionParameters.ValueType.Float;

        exp.networkSynced = false;
        exp.saved = false;
        exp.defaultValue = 0f;

        parameters[index] =
            exp;

        selectedExpParams.parameters =
            parameters.ToArray();

        EditorUtility.SetDirty(
            selectedExpParams
        );
    }

    private void EnsureExpressionBoolParameter(
        string parameter,
        bool networkSynced)
    {
        var parameters =
            selectedExpParams.parameters?.ToList() ??
            new List<VRCExpressionParameters.Parameter>();

        int index =
            parameters.FindIndex(
                p => p.name == parameter
            );

        VRCExpressionParameters.Parameter expressionParameter;

        if (index >= 0)
        {
            expressionParameter =
                parameters[index];
        } else
        {
            expressionParameter =
                new VRCExpressionParameters.Parameter
                {
                    name = parameter
                };

            parameters.Add(
                expressionParameter
            );

            index =
                parameters.Count - 1;
        }

        expressionParameter.valueType =
            VRCExpressionParameters.ValueType.Bool;

        expressionParameter.networkSynced =
            networkSynced;

        expressionParameter.saved = false;
        expressionParameter.defaultValue = 0f;

        parameters[index] =
            expressionParameter;

        selectedExpParams.parameters =
            parameters.ToArray();

        EditorUtility.SetDirty(
            selectedExpParams
        );
    }

    private void EnsureAnimatorParameter(
        string parameter,
        AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] matches =
            targetController.parameters
                .Where(
                    p =>
                        p.name ==
                        parameter
                )
                .ToArray();

        if (
            matches.Length == 1 &&
            matches[0].type == type)
        {
            return;
        }

        for (
            int i =
                targetController.parameters.Length - 1;
            i >= 0;
            i--)
        {
            if (
                targetController.parameters[i].name ==
                parameter)
            {
                targetController.RemoveParameter(i);
            }
        }

        targetController.AddParameter(
            parameter,
            type
        );

        EditorUtility.SetDirty(
            targetController
        );
    }


    private float GetToeSplay(
        bool isLeftFoot,
        int toeIndex)
    {
        toeIndex =
            Mathf.Clamp(
                toeIndex,
                0,
                4
            );

        return isLeftFoot
            ? Mathf.Clamp(
                leftFootSplay[toeIndex],
                -180f,
                180f
            )
            : Mathf.Clamp(
                rightFootSplay[toeIndex],
                -180f,
                180f
            );
    }

    private Transform GetAvatarRoot(
        Transform t)
    {
        if (t == null)
        {
            return null;
        }

        Transform current = t;

        while (current != null)
        {
            if (
                current.GetComponent<Animator>() !=
                null)
            {
                return current;
            }

            current =
                current.parent;
        }

        return t.root;
    }

    private static Vector3 GetContinuousEuler(
        Vector3 reference,
        Vector3 newEuler)
    {
        return new Vector3(
            Mathf.DeltaAngle(
                reference.x,
                newEuler.x
            ) +
            reference.x,

            Mathf.DeltaAngle(
                reference.y,
                newEuler.y
            ) +
            reference.y,

            Mathf.DeltaAngle(
                reference.z,
                newEuler.z
            ) +
            reference.z
        );
    }

    private void SaveGeneratedClip(
        AnimationClip clip,
        string clipName)
    {
        SaveOrOverwriteClip(
            clip,
            GetClipPath(clipName)
        );
    }

    private string GetClipPath(
        string clipName)
    {
        string safeName =
            string.Concat(
                clipName.Select(
                    c =>
                        Path.GetInvalidFileNameChars()
                            .Contains(c)
                            ? '_'
                            : c
                )
            );

        return
            $"{clipOutputFolder}/{targetController.name}_{safeName}.anim";
    }

    private static void EnsureFolder(
        string folder)
    {
        if (
            AssetDatabase.IsValidFolder(
                folder))
        {
            return;
        }

        string normalized =
            folder.Replace("\\", "/")
                .TrimEnd('/');

        string[] parts =
            normalized.Split('/');

        if (
            parts.Length == 0 ||
            parts[0] != "Assets")
        {
            return;
        }

        string current =
            "Assets";

        for (
            int i = 1;
            i < parts.Length;
            i++)
        {
            string next =
                current +
                "/" +
                parts[i];

            if (
                !AssetDatabase.IsValidFolder(
                    next))
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

    private static void SaveOrOverwriteClip(
        AnimationClip clip,
        string fullPath)
    {
        string directory =
            Path.GetDirectoryName(
                fullPath
            );

        if (
            !Directory.Exists(
                directory))
        {
            Directory.CreateDirectory(
                directory
            );

            AssetDatabase.Refresh();
        }

        AnimationClip existing =
            AssetDatabase.LoadAssetAtPath
                <AnimationClip>(
                    fullPath
                );

        if (existing != null)
        {
            AssetDatabase.DeleteAsset(
                fullPath
            );
        }

        AssetDatabase.CreateAsset(
            clip,
            fullPath
        );

        AssetDatabase.ImportAsset(
            fullPath,
            ImportAssetOptions.ForceUpdate
        );
    }

    private string GetBonePath(
        Transform t)
    {
        if (t == null)
        {
            return "";
        }

        string path =
            t.name;

        Transform parent =
            t.parent;

        while (parent != null)
        {
            path =
                parent.name +
                "/" +
                path;

            if (
                parent.name.StartsWith(
                    "Armature",
                    StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            parent =
                parent.parent;
        }

        return path;
    }

    private static void DestroyStateMachineRecursive(
        AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null)
        {
            return;
        }

        foreach (
            ChildAnimatorState child
            in stateMachine.states)
        {
            AnimatorState state =
                child.state;

            if (
                state.motion is BlendTree tree)
            {
                DestroyBlendTreeRecursive(
                    tree
                );
            }

            foreach (
                AnimatorStateTransition transition
                in state.transitions)
            {
                Object.DestroyImmediate(
                    transition,
                    true
                );
            }

            Object.DestroyImmediate(
                state,
                true
            );
        }

        foreach (
            AnimatorStateTransition transition
            in stateMachine.anyStateTransitions)
        {
            Object.DestroyImmediate(
                transition,
                true
            );
        }

        foreach (
            AnimatorTransition transition
            in stateMachine.entryTransitions)
        {
            Object.DestroyImmediate(
                transition,
                true
            );
        }

        foreach (
            ChildAnimatorStateMachine child
            in stateMachine.stateMachines)
        {
            DestroyStateMachineRecursive(
                child.stateMachine
            );
        }

        Object.DestroyImmediate(
            stateMachine,
            true
        );
    }

    private static void DestroyBlendTreeRecursive(
        BlendTree tree)
    {
        if (tree == null)
        {
            return;
        }

        foreach (
            ChildMotion child
            in tree.children)
        {
            if (
                child.motion is BlendTree nested)
            {
                DestroyBlendTreeRecursive(
                    nested
                );
            }
        }

        Object.DestroyImmediate(
            tree,
            true
        );
    }

    private void AutoFillBones()
    {
        Animator[] animators =
            FindObjectsOfType<Animator>();

        if (animators.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Auto Fill Failed",
                "No Animator found in the scene.",
                "OK"
            );

            return;
        }

        Transform avatarRoot =
            animators[0].transform;

        int leftFound = 0;
        int rightFound = 0;

        foreach (
            Transform t
            in avatarRoot.GetComponentsInChildren
                <Transform>(true))
        {
            TryMatchToeBone(
                t,
                ref leftFound,
                ref rightFound
            );
        }

        Debug.Log(
            $"[ToeRig] Auto Fill found {leftFound} left and {rightFound} right toe bones."
        );

        Repaint();
    }

    private void TryMatchToeBone(
        Transform t,
        ref int leftFound,
        ref int rightFound)
    {
        string name =
            t.name.ToLowerInvariant()
                .Replace(" ", "");

        string stripped =
            name;

        if (
            stripped.StartsWith("def-") ||
            stripped.StartsWith("mch-") ||
            stripped.StartsWith("org-"))
        {
            stripped =
                stripped.Substring(4);
        }

        bool isLeft =
            stripped.EndsWith(".l") ||
            stripped.EndsWith("_l") ||
            stripped.Contains("left") ||
            stripped.StartsWith("l.") ||
            stripped.StartsWith("l_");

        bool isRight =
            stripped.EndsWith(".r") ||
            stripped.EndsWith("_r") ||
            stripped.Contains("right") ||
            stripped.StartsWith("r.") ||
            stripped.StartsWith("r_");

        if (isLeft == isRight)
        {
            return;
        }

        int toeIndex =
            GetToeIndex(
                stripped
            );

        if (
            toeIndex < 0 ||
            IsIntermediateSegment(
                stripped))
        {
            return;
        }

        Transform[] target =
            isLeft
                ? leftFootBones
                : rightFootBones;

        if (target[toeIndex] != null)
        {
            return;
        }

        target[toeIndex] = t;

        if (isLeft)
        {
            leftFound++;
        } else
        {
            rightFound++;
        }
    }

    private int GetToeIndex(
        string name)
    {
        if (
            name.Contains("big") ||
            name.Contains("hallux") ||
            name.Contains("thumb"))
        {
            return 0;
        }

        if (
            name.Contains("index") ||
            name.Contains("long"))
        {
            return 1;
        }

        if (
            name.Contains("middle") ||
            name.Contains("mid"))
        {
            return 2;
        }

        if (
            name.Contains("ring") ||
            name.Contains("fourth"))
        {
            return 3;
        }

        if (
            name.Contains("little") ||
            name.Contains("pinky") ||
            name.Contains("small") ||
            name.Contains("baby"))
        {
            return 4;
        }

        for (
            int i = 0;
            i < name.Length - 2;
            i++)
        {
            if (
                name.Substring(i, 3) !=
                "toe")
            {
                continue;
            }

            int j = i + 3;

            if (
                j < name.Length &&
                name[j] == 's')
            {
                j++;
            }

            while (
                j < name.Length &&
                (
                    name[j] == '_' ||
                    name[j] == '.' ||
                    name[j] == '-' ||
                    name[j] == '0'))
            {
                j++;
            }

            string remaining =
                name.Substring(j);

            if (
                remaining.StartsWith(
                    "left"))
            {
                j += 4;
            } else if (
                  remaining.StartsWith(
                      "right"))
            {
                j += 5;
            }

            while (
                j < name.Length &&
                (
                    name[j] == '_' ||
                    name[j] == '.' ||
                    name[j] == '-' ||
                    name[j] == '0'))
            {
                j++;
            }

            if (
                j < name.Length &&
                char.IsDigit(name[j]))
            {
                int digit =
                    name[j] - '0';

                if (
                    digit >= 1 &&
                    digit <= 5)
                {
                    return digit - 1;
                }
            }
        }

        return -1;
    }

    private bool IsIntermediateSegment(
        string name)
    {
        if (
            name.Contains("distal") ||
            name.Contains("intermediate") ||
            name.Contains("proximal2") ||
            name.Contains("phalanx2") ||
            name.Contains("phalanx3") ||
            name.EndsWith("_end") ||
            name.EndsWith(".end"))
        {
            return true;
        }

        string[] sidePatterns =
        {
            "",
            ".l",
            ".r",
            "_l",
            "_r"
        };

        for (
            int segment = 2;
            segment <= 5;
            segment++)
        {
            foreach (
                string side
                in sidePatterns)
            {
                if (
                    name.EndsWith(
                        $".{segment:D2}{side}") ||
                    name.EndsWith(
                        $"_{segment:D2}{side}"))
                {
                    return true;
                }
            }
        }

        return false;
    }


    // ---------------------------------------------------------------------
    // Integrated 4-bit toe network codec
    // ---------------------------------------------------------------------

    private const int EncodedLevels = 15;
    private const int EncodedMiddle = 7;
    private const int NeutralGrayCode = 4; // Gray(7) = 0100

    public static readonly string[] LogicalParameters =
    {
        "ToeLeft1Float",
        "ToeLeft2Float",
        "ToeLeft5Float",
        "ToeRight1Float",
        "ToeRight2Float",
        "ToeRight5Float",

        "ToeSplayLeft1Float",
        "ToeSplayLeft5Float",
        "ToeSplayRight1Float",
        "ToeSplayRight5Float",
    };

    public static void Install(
        AnimatorController controller,
        VRCExpressionParameters expressionParameters)
    {
        if (controller == null)
        {
            Debug.LogError("[ToeRig] Cannot install 4-bit codec: AnimatorController is null.");
            return;
        }

        if (expressionParameters == null)
        {
            Debug.LogError("[ToeRig] Cannot install 4-bit codec: VRCExpressionParameters is null.");
            return;
        }

        string controllerPath = AssetDatabase.GetAssetPath(controller);

        if (string.IsNullOrEmpty(controllerPath))
        {
            Debug.LogError("[ToeRig] Cannot install 4-bit codec: controller has no asset path.");
            return;
        }

        EnsureAnimatorBoolParameter(controller, "IsLocal");

        foreach (string logicalParameter in LogicalParameters)
        {
            AddUnsyncedFloatParameter(
                controller,
                expressionParameters,
                logicalParameter
            );

            AddEncodedBoolParameters(
                controller,
                expressionParameters,
                logicalParameter
            );

            string encoderLayer =
                GetEncoderLayerName(logicalParameter);

            string decoderLayer =
                GetDecoderLayerName(logicalParameter);

            RemoveExistingLayer(
                controller,
                encoderLayer
            );

            RemoveExistingLayer(
                controller,
                decoderLayer
            );

            GenerateFourBitEncoderLayer(
                controller,
                logicalParameter,
                encoderLayer,
                controllerPath
            );

            GenerateFourBitDecoderLayer(
                controller,
                logicalParameter,
                decoderLayer,
                controllerPath
            );
        }

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(expressionParameters);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            controllerPath,
            ImportAssetOptions.ForceUpdate
        );
        AssetDatabase.Refresh();

        Debug.Log(
            "[ToeRig] Installed 4-bit network codec for 10 toe analog channels: " +
            "6 curl Floats + 4 splay Floats = 40 synced Bool bits."
        );
    }

    public static string GetMotionParameter(
        string logicalParameter,
        bool useOSCSmoothPath)
    {
        return useOSCSmoothPath
            ? "OSCm/Proxy/" + logicalParameter
            : logicalParameter;
    }

    private static string GetEncoderLayerName(
        string logicalParameter)
    {
        return "ToeCodec_" +
            logicalParameter +
            "_4BitEncode";
    }

    private static string GetDecoderLayerName(
        string logicalParameter)
    {
        return "ToeCodec_" +
            logicalParameter +
            "_4BitDecode";
    }

    private static string GetBitParameterName(
        string logicalParameter,
        int bit)
    {
        return logicalParameter +
            "_B" +
            bit;
    }

    private static void AddUnsyncedFloatParameter(
        AnimatorController controller,
        VRCExpressionParameters expressionParameters,
        string parameter)
    {
        AnimatorControllerParameter[] animatorMatches =
            controller.parameters
                .Where(p => p.name == parameter)
                .ToArray();

        if (animatorMatches.Length == 0)
        {
            controller.AddParameter(
                parameter,
                AnimatorControllerParameterType.Float
            );
        } else if (
              animatorMatches.Length != 1 ||
              animatorMatches[0].type !=
                  AnimatorControllerParameterType.Float)
        {
            Debug.LogError(
                $"[ToeRig] Animator parameter '{parameter}' must be a single Float parameter."
            );
            return;
        }

        var parameters =
            expressionParameters.parameters?.ToList() ??
            new List<VRCExpressionParameters.Parameter>();

        int index =
            parameters.FindIndex(
                p => p.name == parameter
            );

        VRCExpressionParameters.Parameter expressionParameter;

        if (index >= 0)
        {
            expressionParameter =
                parameters[index];
        } else
        {
            expressionParameter =
                new VRCExpressionParameters.Parameter
                {
                    name = parameter
                };

            parameters.Add(expressionParameter);
            index = parameters.Count - 1;
        }

        expressionParameter.valueType =
            VRCExpressionParameters.ValueType.Float;

        expressionParameter.networkSynced = false;
        expressionParameter.saved = false;
        expressionParameter.defaultValue = 0f;

        parameters[index] =
            expressionParameter;

        expressionParameters.parameters =
            parameters.ToArray();

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(expressionParameters);
    }

    private static void AddEncodedBoolParameters(
        AnimatorController controller,
        VRCExpressionParameters expressionParameters,
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
                controller,
                bitParameter
            );

            var parameters =
                expressionParameters.parameters?.ToList() ??
                new List<VRCExpressionParameters.Parameter>();

            int index =
                parameters.FindIndex(
                    p => p.name == bitParameter
                );

            VRCExpressionParameters.Parameter parameter;

            if (index >= 0)
            {
                parameter =
                    parameters[index];
            } else
            {
                parameter =
                    new VRCExpressionParameters.Parameter
                    {
                        name = bitParameter
                    };

                parameters.Add(parameter);
                index = parameters.Count - 1;
            }

            parameter.valueType =
                VRCExpressionParameters.ValueType.Bool;

            parameter.networkSynced = true;
            parameter.saved = false;
            parameter.defaultValue = 0f;

            parameters[index] =
                parameter;

            expressionParameters.parameters =
                parameters.ToArray();
        }

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(expressionParameters);
    }

    private static void EnsureAnimatorBoolParameter(
        AnimatorController controller,
        string parameter)
    {
        AnimatorControllerParameter[] matches =
            controller.parameters
                .Where(p => p.name == parameter)
                .ToArray();

        if (
            matches.Length == 1 &&
            matches[0].type ==
                AnimatorControllerParameterType.Bool)
        {
            return;
        }

        while (
            controller.parameters.Any(
                p => p.name == parameter
            ))
        {
            AnimatorControllerParameter existing =
                controller.parameters.First(
                    p => p.name == parameter
                );

            controller.RemoveParameter(existing);
        }

        controller.AddParameter(
            parameter,
            AnimatorControllerParameterType.Bool
        );

        EditorUtility.SetDirty(controller);
    }

    private static void GenerateFourBitEncoderLayer(
        AnimatorController controller,
        string rawSourceFloatParameter,
        string layerName,
        string controllerPath)
    {
        if (
            rawSourceFloatParameter.StartsWith(
                "OSCm/Proxy/",
                StringComparison.Ordinal))
        {
            Debug.LogError(
                $"[ToeRig] Refusing to encode OSCmooth proxy '{rawSourceFloatParameter}'. " +
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
                        100f +
                            90f *
                            (quantizedIndex / 4),
                        0f
                    )
                );

            state.writeDefaultValues = true;

            var driver =
                state.AddStateMachineBehaviour
                    <VRCAvatarParameterDriver>();

            driver.localOnly = false;

            driver.parameters =
                new List<
                    VRC_AvatarParameterDriver.Parameter>();

            for (int bit = 0; bit < 4; bit++)
            {
                bool bitSet =
                    (wireCode &
                        (1 << bit)) != 0;

                driver.parameters.Add(
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        name =
                            GetBitParameterName(
                                rawSourceFloatParameter,
                                bit
                            ),

                        type =
                            VRC_AvatarParameterDriver
                                .ChangeType
                                .Set,

                        value =
                            bitSet
                                ? 1f
                                : 0f
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

            if (
                quantizedIndex <
                EncodedLevels - 1)
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
            controller.layers.ToList();

        layers.Add(layer);

        controller.layers =
            layers.ToArray();

        EditorUtility.SetDirty(idleState);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
    }

    private static void GenerateFourBitDecoderLayer(
        AnimatorController controller,
        string decodedFloatParameter,
        string layerName,
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

        for (
            int wireCode = 0;
            wireCode < 16;
            wireCode++)
        {
            int quantizedIndex =
                WireCodeToQuantizedIndex(
                    wireCode
                );

            bool valid =
                quantizedIndex >= 0 &&
                quantizedIndex <
                    EncodedLevels;

            float decodedValue =
                valid
                    ? (
                        quantizedIndex -
                        EncodedMiddle
                    ) /
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
                        260f *
                            (wireCode % 4),
                        100f +
                            90f *
                            (wireCode / 4),
                        0f
                    )
                );

            state.writeDefaultValues = true;

            var driver =
                state.AddStateMachineBehaviour
                    <VRCAvatarParameterDriver>();

            driver.localOnly = false;

            driver.parameters =
                new List<
                    VRC_AvatarParameterDriver.Parameter>
                {
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        name =
                            decodedFloatParameter,

                        type =
                            VRC_AvatarParameterDriver
                                .ChangeType
                                .Set,

                        value =
                            decodedValue
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
                    (wireCode &
                        (1 << bit)) != 0;

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
            controller.layers.ToList();

        layers.Add(layer);

        controller.layers =
            layers.ToArray();

        EditorUtility.SetDirty(idleState);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
    }

    private static void AddForcedBoolCondition(
        AnimatorStateTransition transition,
        string parameterName,
        bool expectedValue)
    {
        AnimatorCondition[] retainedConditions =
            transition.conditions
                .Where(
                    c =>
                        c.parameter !=
                        parameterName
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

        EditorUtility.SetDirty(transition);
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

        return gray ^
            NeutralGrayCode;
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

    private static void RemoveExistingLayer(
        AnimatorController controller,
        string layerName)
    {
        var layers =
            controller.layers.ToList();

        AnimatorControllerLayer existing =
            layers.FirstOrDefault(
                l =>
                    l.name ==
                    layerName
            );

        if (existing == null)
        {
            return;
        }

        layers.Remove(existing);
        controller.layers =
            layers.ToArray();

        if (existing.stateMachine != null)
        {
            DestroyStateMachineRecursive(
                existing.stateMachine
            );
        }

        EditorUtility.SetDirty(controller);
    }

}

static class Prefs
{
    public static void SetObject(
        string key,
        UnityEngine.Object obj)
    {
        if (obj == null)
        {
            EditorPrefs.DeleteKey(
                key
            );

            return;
        }

        string path =
            AssetDatabase.GetAssetPath(
                obj
            );

        string guid =
            AssetDatabase.AssetPathToGUID(
                path
            );

        EditorPrefs.SetString(
            key,
            guid
        );
    }

    public static T GetObject<T>(
        string key)
        where T : UnityEngine.Object
    {
        if (
            !EditorPrefs.HasKey(
                key))
        {
            return null;
        }

        string guid =
            EditorPrefs.GetString(
                key
            );

        string path =
            AssetDatabase.GUIDToAssetPath(
                guid
            );

        return AssetDatabase.LoadAssetAtPath<T>(
            path
        );
    }
}

static class BonePrefs
{
    private static Transform GetRoot(
        Transform t)
    {
        if (t == null)
        {
            return null;
        }

        Transform root = t;

        while (root.parent != null)
        {
            root =
                root.parent;
        }

        return root;
    }

    public static void SaveBone(
        string key,
        Transform t)
    {
        if (t == null)
        {
            EditorPrefs.DeleteKey(
                key
            );

            return;
        }

        Transform root =
            GetRoot(t);

        EditorPrefs.SetString(
            key,
            GetPathRelativeToRoot(
                t,
                root
            )
        );
    }

    public static Transform LoadBone(
        string key)
    {
        if (
            !EditorPrefs.HasKey(
                key))
        {
            return null;
        }

        string path =
            EditorPrefs.GetString(
                key
            );

        string leafName =
            path.Split('/')[^1];

        foreach (
            Transform t
            in GameObject.FindObjectsOfType
                <Transform>())
        {
            if (
                t.name == leafName &&
                GetPathRelativeToRoot(
                    t,
                    GetRoot(t)
                ) ==
                path)
            {
                return t;
            }
        }

        return null;
    }

    private static string GetPathRelativeToRoot(
        Transform t,
        Transform root)
    {
        if (t == root)
        {
            return "";
        }

        string path =
            t.name;

        Transform parent =
            t.parent;

        while (
            parent != null &&
            parent != root)
        {
            path =
                parent.name +
                "/" +
                path;

            parent =
                parent.parent;
        }

        return path;
    }
}



