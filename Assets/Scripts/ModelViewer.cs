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
using Newtonsoft.Json;

// sample avatar
// Feminine https://models.readyplayer.me/645938677cf7d03f60e0b4e3.glb /1.73628
// Masculine https://models.readyplayer.me/6423ac9aa9cf14ab7e456f88.glb /1.86452
public class ModelViewer : MonoBehaviour
{
    public class LoadAvatarData
    {
        public string id;
        public string name;
        public string status;
        public string url;
        public Vector3 position;
    }
    public class RunAnimationData
    {
        public string id;
        public int animationId;
    }

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
    }

    private void OnDestroy()
    {
        playableGraph.Destroy();
    }

    //// Update is called once per frame
    //void Update()
    //{

    //}


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
        onAvatarLoadCompleted(avatarName);
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

    public void LoadAvatar(string json)
    {
        var data = JsonUtility.FromJson<LoadAvatarData>(json);
        StartCoroutine(DownloadAvatar(data.id, data.url, data.name, data.status, data.position));
    }

    public void SetPositionAvatar(string json)
    {
        var data = JsonUtility.FromJson<LoadAvatarData>(json);
        if (avatarDict.TryGetValue(data.id, out var avatar))
        {
            avatar.transform.position = data.position;
        }
    }

    public void RunAnimation(string json)
    {
        var data = JsonUtility.FromJson<RunAnimationData>(json);
        if (avatarDict.TryGetValue(data.id, out var avatar))
        {
            var clip = clips[data.animationId];
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

    public void SetPositionCamera(string json)
    {
        var newPos = JsonUtility.FromJson<Vector3>(json);
        mainCamera.transform.position = newPos;
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
}

