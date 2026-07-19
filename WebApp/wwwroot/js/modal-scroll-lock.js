window.whistModalLock = (() => {
    let lockCount = 0;
    let scrollY = 0;

    function lock() {
        lockCount += 1;
        if (lockCount > 1) return;

        scrollY = window.scrollY || document.documentElement.scrollTop || 0;
        document.body.style.position = "fixed";
        document.body.style.top = `-${scrollY}px`;
        document.body.style.left = "0";
        document.body.style.right = "0";
        document.body.style.width = "100%";
        document.body.style.overflow = "hidden";
    }

    function unlock() {
        if (lockCount === 0) return;

        lockCount -= 1;
        if (lockCount > 0) return;

        document.body.style.position = "";
        document.body.style.top = "";
        document.body.style.left = "";
        document.body.style.right = "";
        document.body.style.width = "";
        document.body.style.overflow = "";
        window.scrollTo(0, scrollY);
    }

    return { lock, unlock };
})();
