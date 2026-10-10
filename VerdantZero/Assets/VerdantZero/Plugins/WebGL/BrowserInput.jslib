// Browser tweaks for the Verdant Zero WebGL build.
mergeInto(LibraryManager.library, {
    // Right click launches seeds: without this the browser opens its context menu,
    // the page loses focus and the game pauses.
    VerdantZero_DisableContextMenu: function () {
        document.addEventListener('contextmenu', function (e) { e.preventDefault(); });
    },

    // 1 if the page is in browser full screen.
    VerdantZero_IsFullscreen: function () {
        return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
    },

    // Enters or leaves browser full screen. The page template (window.verdantZeroFullscreen) queues
    // the request for the next click / tap / key press when the browser needs a user gesture.
    VerdantZero_SetFullscreen: function (on) {
        if (window.verdantZeroFullscreen) {
            window.verdantZeroFullscreen(!!on);
            return;
        }
        if (on) {
            var el = document.documentElement;
            var req = el.requestFullscreen || el.webkitRequestFullscreen;
            if (req) { try { var p = req.call(el); if (p && p.catch) p.catch(function () {}); } catch (e) {} }
        } else if (document.fullscreenElement || document.webkitFullscreenElement) {
            var exit = document.exitFullscreen || document.webkitExitFullscreen;
            if (exit) { try { var q = exit.call(document); if (q && q.catch) q.catch(function () {}); } catch (e) {} }
        }
    }
});
