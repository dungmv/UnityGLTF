using System.Runtime.InteropServices;
using Newtonsoft.Json;
using ReadyPlayerMe.AvatarLoader;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityGLTF;
using WrapMode = UnityEngine.WrapMode;

public class AnimatorMaker : MonoBehaviour
{
    [SerializeField] [Tooltip("RPM avatar URL or shortcode to load")]
    private string avatarUrl;

    private GameObject avatar;
    private AvatarObjectLoader avatarObjectLoader;

    [SerializeField] [Tooltip("Animator to use on loaded avatar")]
    private RuntimeAnimatorController animatorController;

    [SerializeField] [Tooltip("If true it will try to load avatar from avatarUrl on start")]
    private bool loadOnStart = true;

    [SerializeField]
    [Tooltip("Preview avatar to display until avatar loads. Will be destroyed after new avatar is loaded")]
    private GameObject previewAvatar;

    [SerializeField] private AnimationClip[] clips;
    [SerializeField] private Animator animator;

    private PlayableGraph playableGraph;
    private AnimationClipPlayable playableClip;

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

        if (previewAvatar != null)
        {
            SetupAvatar(previewAvatar);
        }

        if (loadOnStart)
        {
            LoadAvatar(avatarUrl);
        }
        
        PlayAnimationById(0);

        var animNames = new string[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            animNames[i] = clips[i].name;
        }
// #if !UNITY_EDITOR && UNITY_WEBGL
        OnInitialized(JsonConvert.SerializeObject(animNames));
        WebGLInput.captureAllKeyboardInput = false;
// #endif
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
        if (previewAvatar != null)
        {
            Destroy(previewAvatar);
            previewAvatar = null;
        }

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

    public void LoadAvatar(string url)
    {
        //remove any leading or trailing spaces
        avatarUrl = url.Trim(' ');
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
        playableClip = AnimationClipPlayable.Create(playableGraph, clip);
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
                var clip = clips[i];
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
}

