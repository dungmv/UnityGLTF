mergeInto(LibraryManager.library, {
    AnimationSelected: function (id) {
        window.dispatchReactUnityEvent("AnimationSelected", id);
    },
    OnAvatarLoadCompleted: function(url) {
        window.dispatchReactUnityEvent("OnAvatarLoadCompleted", UTF8ToString(url));
    },
    OnAvatarLoadFailed: function(url, msg) {
        window.dispatchReactUnityEvent("OnAvatarLoadFailed", UTF8ToString(url), UTF8ToString(msg));
    }
});
