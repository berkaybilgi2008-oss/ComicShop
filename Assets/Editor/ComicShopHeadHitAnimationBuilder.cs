using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public static class ComicShopHeadHitAnimationBuilder
{
    private const string Folder = "Assets/Animations";
    private const string ClipPath = Folder + "/HeadHit_OverTheTop.anim";

    [MenuItem("ComicShop/Animation/Create Over-The-Top Head Hit")]
    public static void Build()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (player == null)
        {
            EditorUtility.DisplayDialog("ComicShop", "Assets/Prefabs/Player.prefab bulunamadı.", "OK");
            return;
        }

        var animator = player.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            EditorUtility.DisplayDialog("ComicShop", "Player prefabında Animator veya Animator Controller bulunamadı.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Animations");

        // Exaggerated cartoon fall: the whole visual snaps back, rises slightly,
        // then rotates through a full 180 degrees so the feet point dramatically upward.
        float[] t = { 0f, .06f, .12f, .22f, .34f, .48f, .64f, .78f, .92f };
        float[] z = { 0f, -10f, -42f, -92f, -135f, -165f, -180f, -186f, -180f };
        float[] y = { 0f, 0f, .03f, .10f, .18f, .16f, .08f, .02f, 0f };

        var clip = new AnimationClip
        {
            name = "HeadHit_OverTheTop",
            frameRate = 60,
            legacy = false,
            wrapMode = WrapMode.Once
        };

        AddQuaternionCurves(clip, "", t, z);
        AddCurve(clip, "", "m_LocalPosition.y", t, y);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.stopTime = .92f;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.DeleteAsset(ClipPath);
        AssetDatabase.CreateAsset(clip, ClipPath);

        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            EditorUtility.DisplayDialog("ComicShop",
                "Animator Controller AnimatorController tipinde değil; mevcut controller'a state eklenemedi.",
                "OK");
            return;
        }

        EnsureParameter(controller, "HeadHit", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        AnimatorState locomotion = FindState(sm, "Locomotion") ?? sm.defaultState;
        AnimatorState head = FindState(sm, "HeadHit_OverTheTop") ?? sm.AddState("HeadHit_OverTheTop");

        head.motion = clip;
        head.speed = 1f;

        bool anyExists = false;
        foreach (var tr in sm.anyStateTransitions)
        {
            if (tr.destinationState == head)
            {
                anyExists = true;
                break;
            }
        }

        if (!anyExists)
        {
            var tr = sm.AddAnyStateTransition(head);
            tr.hasExitTime = false;
            tr.duration = .02f;
            tr.AddCondition(AnimatorConditionMode.If, 0, "HeadHit");
        }

        bool backExists = false;
        foreach (var tr in head.transitions)
        {
            if (tr.destinationState == locomotion)
            {
                backExists = true;
                tr.hasExitTime = true;
                tr.exitTime = .98f;
                tr.duration = .12f;
                tr.hasFixedDuration = true;
                backExists = true;
                break;
            }
        }

        if (!backExists)
        {
            var tr = head.AddTransition(locomotion);
            tr.hasExitTime = true;
            tr.exitTime = .98f;
            tr.duration = .12f;
            tr.hasFixedDuration = true;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = clip;
        EditorGUIUtility.PingObject(clip);
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (var child in sm.states)
            if (child.state.name == name) return child.state;
        return null;
    }

    private static void EnsureParameter(
        AnimatorController controller,
        string name,
        AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
            if (p.name == name) return;
        controller.AddParameter(name, type);
    }

    private static void AddQuaternionCurves(
        AnimationClip clip,
        string path,
        float[] times,
        float[] zDegrees)
    {
        var qx = new AnimationCurve();
        var qy = new AnimationCurve();
        var qz = new AnimationCurve();
        var qw = new AnimationCurve();

        for (int i = 0; i < times.Length; i++)
        {
            Quaternion q = Quaternion.Euler(0f, 0f, zDegrees[i]);
            qx.AddKey(new Keyframe(times[i], q.x));
            qy.AddKey(new Keyframe(times[i], q.y));
            qz.AddKey(new Keyframe(times[i], q.z));
            qw.AddKey(new Keyframe(times[i], q.w));
        }

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), qx);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), qy);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), qz);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), qw);

        clip.EnsureQuaternionContinuity();
    }

    private static void AddCurve(
        AnimationClip clip,
        string path,
        string property,
        float[] times,
        float[] values)
    {
        var curve = new AnimationCurve();
        for (int i = 0; i < times.Length; i++)
            curve.AddKey(new Keyframe(times[i], values[i]));

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), property),
            curve);
    }
}
