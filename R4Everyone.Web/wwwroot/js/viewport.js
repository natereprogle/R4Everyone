window.viewport = {
    register: function (dotNetRef) {
        function notify() {
            dotNetRef.invokeMethodAsync('OnResize', window.innerWidth, window.innerHeight);
        }

        notify();
        window.addEventListener('resize', notify);
    }
};
