mergeInto(LibraryManager.library, {
    AnimationSelected: function (id) {
        window.dispatchReactUnityEvent("AnimationSelected", id);
    },
    OnAvatarLoadCompleted: function(url) {
        window.dispatchReactUnityEvent("OnAvatarLoadCompleted", UTF8ToString(url));
    },
    OnAvatarLoadFailed: function(url, msg) {
        window.dispatchReactUnityEvent("OnAvatarLoadFailed", UTF8ToString(url), UTF8ToString(msg));
    },
    OnAvatarCombineCompleted: function(data, size) {
        const bytes = new Uint8Array(size);
        for (var i = 0; i < size; i++)
        {
            bytes[i] = HEAPU8[data + i];
        }
        window.dispatchReactUnityEvent("OnAvatarCombineCompleted", bytes, size);
    }
});
