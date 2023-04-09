using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ReadyPlayerMe.AvatarLoader;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

public class AnimatorMaker : MonoBehaviour
{
    [SerializeField]
    [Tooltip("RPM avatar URL or shortcode to load")]
    private string avatarUrl;
    private GameObject avatar;
    private AvatarObjectLoader avatarObjectLoader;
    [SerializeField]
    [Tooltip("Animator to use on loaded avatar")]
    private RuntimeAnimatorController animatorController;
    [SerializeField]
    [Tooltip("If true it will try to load avatar from avatarUrl on start")]
    private bool loadOnStart = true;
    [SerializeField]
    [Tooltip("Preview avatar to display until avatar loads. Will be destroyed after new avatar is loaded")]
    private GameObject previewAvatar;
    [SerializeField]
    private AnimationClip[] clips;
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private PlayableGraph playableGraph;
    [SerializeField]
    private RectTransform animationContainer;
    [SerializeField]
    private Button prefabItem;

    private AnimationClipPlayable currentAnimation;

#if UNITY_WEBGL
    [DllImport("__Internal")]
    private static extern void AnimationSelected(int id);
    [DllImport("__Internal")]
    private static extern void OnAvatarLoadCompleted(string url);
    [DllImport("__Internal")]
    private static extern void OnAvatarLoadFailed(string url, string msg);
#endif

    private void Start()
    {
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

        for (int i = 0; i < clips.Length; i++)
        {
            int animId = i;
            var clip = clips[animId];
            var item = Instantiate(prefabItem, Vector3.zero, Quaternion.identity, animationContainer);
            item.gameObject.SetActive(true);
            item.gameObject.name = clip.name;
            item.GetComponentInChildren<TMP_Text>().text = clip.name;
            item.onClick.AddListener(() => { PlayAnimationById(animId); });
        }
#if !UNITY_EDITOR && UNITY_WEBGL
     WebGLInput.captureAllKeyboardInput = false;
#endif
    }

    private void OnEnable()
    {
        playableGraph = PlayableGraph.Create();
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        //TouchSimulation.Enable();
        //EnhancedTouchSupport.Enable
        //UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove += OnTouchMove;
    }

    private void OnDisable()
    {
        //UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove -= OnTouchMove;
        //TouchSimulation.Disable();
        //EnhancedTouchSupport.Disable();
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
#if UNITY_WEBGL && !UNITY_EDITOR
        AnimationSelected(id);
#endif
    }

    public void PlayAnimation(AnimationClip clip)
    {
        var playableOutput = AnimationPlayableOutput.Create(playableGraph, "AnimationClip", animator);
        // Wrap the clip in a playable
        clip.wrapMode = WrapMode.Loop;
        var clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
        // Connect the Playable to an output
        playableOutput.SetSourcePlayable(clipPlayable); 
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
    
    public void Combine()
    {
        AnimationClip[] animations = AnimationUtility.GetAnimationClips(avatar);
        var export = new GLTFast.GltfImport();
 
    }
}
