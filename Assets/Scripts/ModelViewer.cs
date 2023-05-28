using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using ReadyPlayerMe.AvatarLoader;
using UnityEngine.Networking;
using GLTFast;
using UnityEngine.Animations;
using UnityEngine.Playables;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine.Events;

// sample avatar
// Feminine https://models.readyplayer.me/645938677cf7d03f60e0b4e3.glb /1.73628
// Masculine https://models.readyplayer.me/6423ac9aa9cf14ab7e456f88.glb /1.86452
public class ModelViewer : MonoBehaviour
{
    private static ModelViewer _self;

    [SerializeField] private Camera mainCamera;
    [SerializeField] private AnimationClip[] clips;
    [SerializeField] private AvatarController avatarPrefab;

    private Dictionary<string, AvatarController> avatarDict;
    private PlayableGraph playableGraph;
    static private Quaternion defaultRotation = Quaternion.Euler(0, 180, 0); 
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    public static extern void onAvatarLoadCompleted(string url);
    [DllImport("__Internal")]
    public static extern void onInitialized();
#endif
    // Use this for initialization
    void Start()
    {
        _self = this;
        avatarDict = new Dictionary<string, AvatarController>();
        playableGraph = PlayableGraph.Create();
#if UNITY_EDITOR
        StartCoroutine(DownloadAvatar(
            "6423ac9aa9cf14ab7e456f88",
            "https://models.readyplayer.me/6423ac9aa9cf14ab7e456f88.glb",
            "dungmv",
            "happy",
            new Vector3(0, 0, 0)
        ));

        SetBackgroundColor("#00FF00");
#endif
#if UNITY_IOS && !UNITY_EDITOR
        onInitialized();
#endif
#if UNITY_IOS
        registerLoadAvatarDelegate(LoadAvatarCallback);
        registerSetBackgroundColorDelegate(SetBackgroundColorCallback);
        registerSetFoVDelegate(SetFoVCallback);
        registerRunAnimationDelegate(RunAnimationCallback);
        registerSetPositionAvatarDelegate(SetPositionAvatarCallback);
        registerSetPositionCameraDelegate(SetPositionCameraCallback);
#endif
    }

    private void OnDestroy()
    {
        playableGraph.Destroy();
        _self = null;
    }
    
    private IEnumerator DownloadAvatar(string avatarId, string url, string avatarName, string avatarStatus, Vector3 position)
    {
        using (var webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (!string.IsNullOrWhiteSpace(webRequest.error))
            {
                Debug.LogError($"Error {webRequest.responseCode} - {webRequest.error}");
                yield break;
            }
            ImportAvatar(webRequest.downloadHandler.data, avatarId, avatarName, avatarStatus, position);
        }
    }

    private async void ImportAvatar(byte[] bytes, string avatarId, string avatarName, string avatarStatus, Vector3 position)
    {
        GltfImport gltf = new GltfImport();
        if (await gltf.LoadGltfBinary(bytes))
        {
            var go = Instantiate(avatarPrefab);
            GameObject avatar = new GameObject(avatarId, new Type[] { typeof (Animator) });
            go.transform.position = position;
            go.avatar = avatar;
            avatar.transform.parent = go.transform;
            avatar.transform.position = Vector3.zero;
            avatar.transform.rotation = defaultRotation;
            avatar.SetActive(true);
            GltFastGameObjectInstantiator customInstantiator = new GltFastGameObjectInstantiator(gltf, avatar.transform);
            await gltf.InstantiateMainSceneAsync(customInstantiator);

            // setup animator
            var animator = avatar.GetComponent<Animator>();
            animator.avatar = AnimatorFromAvatar(avatar);
            animator.applyRootMotion = true;

            // setup name and status
            go.textName.text = avatarName;
            go.textStatus.text = avatarStatus;
            // save
            avatarDict[avatarId] = go;
        }
#if UNITY_IOS && !UNITY_EDITOR
        onAvatarLoadCompleted(avatarId);
#endif
    }

    private Avatar AnimatorFromAvatar(GameObject model)
    {
        // val.avatar = AvatarBuilder.BuildHumanAvatar(model, new HumanDescription()
        // {
        //     human = humanBones,
        // });
        var gender = DetectGender(model);
        string text = ((gender == OutfitGender.Masculine) ?
            "AnimationAvatars/MasculineAnimationAvatar" :
            "AnimationAvatars/FeminineAnimationAvatar");
        return Resources.Load<Avatar>(text);
    }

    private OutfitGender DetectGender(GameObject avatar)
    {
        var headTop = avatar.transform.Find("Armature/Hips/Spine/Spine1/Spine2/Neck/Head/HeadTop_End");
        Debug.Log(headTop.position.y);
        return headTop.position.y > 1.8f ? OutfitGender.Masculine : OutfitGender.Feminine;
    }

    // call from ios/android
    public void LoadAvatar(string id, string url, string avatarName,string status, float x, float y, float z)
    {
        StartCoroutine(DownloadAvatar(id, url, avatarName, status, new Vector3(x, y, z)));
    }

    public void SetPositionAvatar(string avatarId, float x, float y, float z)
    {
        if (avatarDict.TryGetValue(avatarId, out var avatar))
        {
            avatar.transform.position = new Vector3(x, y, z);
        }
    }

    public void RunAnimation(string avatarId, int animationId)
    {
        if (avatarDict.TryGetValue(avatarId, out var avatar))
        {
            var clip = clips[animationId];
            var animator = avatar.avatar.GetComponent<Animator>();
            var playableOutput = AnimationPlayableOutput.Create(playableGraph, "AnimationClip", animator);
            // Wrap the clip in a playable
            clip.wrapMode = WrapMode.Loop;
            var playableClip = AnimationClipPlayable.Create(playableGraph, clip);
            // Connect the Playable to an output
            playableOutput.SetSourcePlayable(playableClip);
            // Plays the Graph.
            playableGraph.Play();
        }
    }

    public void SetPositionCamera(float x, float y, float z)
    {
        mainCamera.transform.position = new Vector3(x, y, z);
    }

    public void SetBackgroundColor(string color)
    {
        if (ColorUtility.TryParseHtmlString(color, out var val))
        {
            mainCamera.backgroundColor = val;
        }
    }

    public void SetFoV(float fov)
    {
        mainCamera.fieldOfView = fov;
    }
    
#if UNITY_IOS
    public delegate void LoadAvatarDelegate(string id, string url, string name,string status, float x, float y, float z);
    [DllImport("__Internal")]
    public static extern void registerLoadAvatarDelegate(LoadAvatarDelegate cb);
    [MonoPInvokeCallback(typeof(LoadAvatarDelegate))]
    public static void LoadAvatarCallback(string id, string url, string name,string status, float x, float y, float z)
    {
        if (_self != null)
        {
            _self.LoadAvatar(id, url, name, status, x, y, z);
        }
    }

    public delegate void SetPositionAvatarDelegate(string avatarId, float x, float y, float z);

    [DllImport("__Internal")]
    public static extern void registerSetPositionAvatarDelegate(SetPositionAvatarDelegate cb);
    [MonoPInvokeCallback(typeof(SetPositionAvatarDelegate))]
    public static void SetPositionAvatarCallback(string avatarId, float x, float y, float z)
    {
        if (_self != null)
        {
            _self.SetPositionAvatar(avatarId, x, y, z);
        }
    }
    [DllImport("__Internal")]
    public static extern void registerSetPositionCameraDelegate(UnityAction<float, float, float> cb);
    [MonoPInvokeCallback(typeof(UnityAction<float, float, float>))]
    public static void SetPositionCameraCallback(float x, float y, float z)
    {
        if (_self != null)
        {
            _self.SetPositionCamera(x, y, z);
        }
    }
    
    [DllImport("__Internal")]
    public static extern void registerSetBackgroundColorDelegate(UnityAction<string> cb);
    [MonoPInvokeCallback(typeof(UnityAction<string>))]
    public static void SetBackgroundColorCallback(string color)
    {
        if (_self != null)
        {
            _self.SetBackgroundColor(color);
        }
    }

    [DllImport("__Internal")]
    public static extern void registerSetFoVDelegate(UnityAction<float> cb);
    [MonoPInvokeCallback(typeof(UnityAction<float>))]
    public static void SetFoVCallback(float fov)
    {
        if (_self != null)
        {
            _self.SetFoV(fov);
        }
    }

    [DllImport("__Internal")]
    public static extern void registerRunAnimationDelegate(UnityAction<string, int> cb);

    [MonoPInvokeCallback(typeof(UnityAction<string, int>))]
    public static void RunAnimationCallback(string avatarId, int animationId)
    {
        if (_self != null)
        {
            _self.RunAnimation(avatarId, animationId);
        }
    }
    
#endif
}

