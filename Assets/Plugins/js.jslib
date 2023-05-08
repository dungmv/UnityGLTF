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
    },
    OnGifCompleted: function(data, size) {
        const bytes = new Uint8Array(size);
        for (var i = 0; i < size; i++)
        {
            bytes[i] = HEAPU8[data + i];
        }
        window.dispatchReactUnityEvent("OnGifCompleted", bytes, size);
    },
    OnInitialized: function(animations) {
        window.dispatchReactUnityEvent("OnInitialized", JSON.parse(UTF8ToString(animations)));
    },
    // override default callback
    emscripten_set_wheel_callback_on_thread: function (
        target,
        userData,
        useCapture,
        callbackfunc,
        targetThread
    ) {
        target = findEventTarget(target);
 
        // the fix
        if (!target) {
            return -4;
        }
 
        if (typeof target.onwheel !== 'undefined') {
            registerWheelEventCallback(
                target,
                userData,
                useCapture,
                callbackfunc,
                9,
                'wheel',
                targetThread
            );
            return 0;
        } else {
            return -1;
        }
    }
});
