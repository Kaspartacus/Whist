let highlightResizeHandler = null;
let highlightResizeTimer = null;
let lastHighlightPageSize = null;

const getWidth = () => window.innerWidth || document.documentElement.clientWidth || 0;

const getHighlightColumnCount = () => {
    const grid = document.querySelector(".highlight-grid");

    if (!grid) {
        return 3;
    }

    const columns = window
        .getComputedStyle(grid)
        .gridTemplateColumns
        .split(" ")
        .filter(Boolean);

    return columns.length || 3;
};

const getHighlightPageSize = () => {
    const width = getWidth();

    if (width < 900) {
        return 6;
    }

    const columns = getHighlightColumnCount();
    return Math.max(6, Math.min(columns * 2, 10));
};

const notifyHighlightPageSize = (dotNetRef) => {
    const pageSize = getHighlightPageSize();

    if (pageSize === lastHighlightPageSize) {
        return;
    }

    lastHighlightPageSize = pageSize;
    dotNetRef.invokeMethodAsync("OnHighlightPageSizeChanged", pageSize).catch(() => {});
};

window.whistViewport = {
    getWidth,
    getHighlightPageSize,

    watchHighlightPageSize: (dotNetRef) => {
        if (highlightResizeHandler) {
            window.removeEventListener("resize", highlightResizeHandler);
        }

        lastHighlightPageSize = getHighlightPageSize();

        highlightResizeHandler = () => {
            window.clearTimeout(highlightResizeTimer);
            highlightResizeTimer = window.setTimeout(() => notifyHighlightPageSize(dotNetRef), 150);
        };

        window.addEventListener("resize", highlightResizeHandler);
    },

    unwatchHighlightPageSize: () => {
        if (!highlightResizeHandler) {
            return;
        }

        window.removeEventListener("resize", highlightResizeHandler);
        highlightResizeHandler = null;
        window.clearTimeout(highlightResizeTimer);
        highlightResizeTimer = null;
        lastHighlightPageSize = null;
    }
};
