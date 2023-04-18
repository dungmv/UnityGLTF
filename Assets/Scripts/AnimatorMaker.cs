using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Newtonsoft.Json;
using ReadyPlayerMe.AvatarLoader;
using UnityGIF;
using UnityGIF.Data;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityGLTF;
using WrapMode = UnityEngine.WrapMode;

public class AnimatorMaker : MonoBehaviour
{
    private GameObject avatar;
    private AvatarObjectLoader avatarObjectLoader;

    [SerializeField] [Tooltip("Animator to use on loaded avatar")]
    private RuntimeAnimatorController animatorController;

    [SerializeField] private AnimationClip[] clips;
    [SerializeField] private Animator animator;
    [SerializeField] private Camera mainCamera;

    private PlayableGraph playableGraph;

#if UNITY_WEBGL
    [DllImport("__Internal")]
    private static extern void OnAvatarLoadCompleted(string url);

    [DllImport("__Internal")]
    private static extern void OnAvatarLoadFailed(string url, string msg);

    [DllImport("__Internal")]
    private static extern void OnAvatarCombineCompleted(byte[] data, int size);
    [DllImport("__Internal")]
    private static extern void OnInitialized(string animations);
#endif

    private void Start()
    {
        playableGraph = PlayableGraph.Create();
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        
        avatarObjectLoader = new AvatarObjectLoader();
        avatarObjectLoader.OnCompleted += OnLoadCompleted;
        avatarObjectLoader.OnFailed += OnLoadFailed;

#if !UNITY_EDITOR && UNITY_WEBGL
        var animNames = new string[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            animNames[i] = clips[i].name;
        }
        OnInitialized(JsonConvert.SerializeObject(animNames));
        WebGLInput.captureAllKeyboardInput = false;
#endif
    }

    private void OnDestroy()
    {
        playableGraph.Destroy();
    }

    private void OnLoadFailed(object sender, FailureEventArgs args)
    {
#if !UNITY_EDITOR && UNITY_WEBGL
        OnAvatarLoadFailed(args.Url, args.Message);
#endif
    }

    private void OnLoadCompleted(object sender, CompletionEventArgs args)
    {
        SetupAvatar(args.Avatar);
#if !UNITY_EDITOR && UNITY_WEBGL
        OnAvatarLoadCompleted(args.Url);
#endif
    }

    private void SetupAvatar(GameObject targetAvatar)
    {
        if (avatar != null)
        {
            Destroy(avatar);
        }

        avatar = targetAvatar;
        // Re-parent and reset transforms
        avatar.transform.parent = transform;
        avatar.transform.localPosition = Vector3.zero;
        avatar.transform.localRotation = Quaternion.identity;

        // setup animator
        animator = avatar.GetComponent<Animator>();
        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;
    }

    public void LoadAvatar(string avatarUrl)
    {
        avatarObjectLoader.LoadAvatar(avatarUrl);
    }

    public void PlayAnimationById(int id)
    {
        PlayAnimation(clips[id]);
    }

    public void PlayAnimation(AnimationClip clip)
    {
        var playableOutput = AnimationPlayableOutput.Create(playableGraph, "AnimationClip", animator);
        // Wrap the clip in a playable
        clip.wrapMode = WrapMode.Loop;
        var playableClip = AnimationClipPlayable.Create(playableGraph, clip);
        // Connect the Playable to an output
        playableOutput.SetSourcePlayable(playableClip);
        // Plays the Graph.
        playableGraph.Play();
    }

    //private void OnTouchMove(Finger fi)
    //{
    //    float delta = fi.currentTouch.delta.x;
    //    if (delta != 0)
    //    {
    //        animator.transform.Rotate(new Vector3(0, delta, 0)); 
    //    }
    //}

    public void CombineAvatarWithAnimations(string data)
    {
        var animIds = JsonConvert.DeserializeObject<int[]>(data);
        var options = new ExportOptions();
        options.AfterSceneExport += (sceneExporter, root) =>
        {
            for (var i = 0; i < animIds.Length; i++)
            {
                var clip = clips[animIds[i]];
                sceneExporter.ExportAnimationClip(clip, clip.name, avatar.transform, 1);
            }
        };
        GLTFSceneExporter exporter = new GLTFSceneExporter(avatar.transform, options);
        var bytes = exporter.SaveGLBToByteArray("Avatar");
        Debug.LogFormat("Save file completed " + bytes.Length);
#if UNITY_WEBGL && !UNITY_EDITOR
        OnAvatarCombineCompleted(bytes, bytes.Length);
#endif
    }

    public void CreateGif()
    {
        var goCam = new GameObject();
        var cam = goCam.AddComponent<Camera>();
        cam.CopyFrom(mainCamera);
        cam.enabled = false;

        RenderTexture renderTexture = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 24);
        UnityEngine.Texture2D screenShot = new UnityEngine.Texture2D(cam.pixelWidth, cam.pixelHeight, TextureFormat.RGBA32, false);
        cam.targetTexture = renderTexture;


        var frames = new List<GifFrame>();

        var clip = clips[0];
        var playableGraph = PlayableGraph.Create();
        var animationClipPlayable = (Playable)AnimationClipPlayable.Create(playableGraph, clip);

        var playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", avatar.GetComponent<Animator>());
        playableOutput.SetSourcePlayable(animationClipPlayable);
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        var timeStep = 1.0f / 5.0f;

        var currentRT = RenderTexture.active;
        RenderTexture.active = cam.targetTexture;
        for (int i = 0; i < 7; i++)
        {
            playableGraph.Evaluate(timeStep);

            cam.Render();
            screenShot.ReadPixels(new Rect(0, 0, cam.targetTexture.width, cam.targetTexture.height), 0, 0);
            screenShot.Apply();

            var frame = new GifFrame();
            frame.Delay = timeStep;
            frame.Texture = new UnityGIF.Data.Texture2D(screenShot.width, screenShot.height);
            frame.Texture.SetPixelsFloat(screenShot.GetPixels());
            frame.ApplyPalette(UnityGIF.Enums.MasterPalette.Levels666);
            frames.Add(frame);
        }
        RenderTexture.active = currentRT;

        Gif gif = new Gif(frames);
        var bytes = gif.Encode();
        Debug.Log(bytes.Length);

        playableGraph.Destroy();
        using (FileStream fs = new FileStream("avatar.gif", FileMode.OpenOrCreate))
        {
            BinaryWriter writer = new BinaryWriter(fs);
            writer.Write(bytes);
        }
    }
}

