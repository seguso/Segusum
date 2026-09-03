window.segusumTranslatorScrollTo = async function (id) {
    const targetIndex = Number(id.substring(4));
    const waitForRender = () => new Promise(resolve => requestAnimationFrame(resolve));
    const scroller = document.querySelector("article.content");

    for (let attempt = 0; attempt < 40; attempt++) {
        const element = document.getElementById(id);
        if (element) {
            element.scrollIntoView({ behavior: "smooth", block: "center" });
            return;
        }

        const rows = Array.from(document.querySelectorAll(".catalog-row"))
            .map(row => Number(row.id.substring(4)))
            .filter(Number.isFinite);
        if (!scroller || rows.length === 0) break;

        const first = Math.min(...rows);
        const last = Math.max(...rows);
        const direction = targetIndex < first ? -1 : targetIndex > last ? 1 : 0;
        if (direction === 0) break;
        scroller.scrollTop += direction * Math.max(scroller.clientHeight * 0.8, 300);
        await waitForRender();
    }

    const element = document.getElementById(id);
    if (element) element.scrollIntoView({ behavior: "smooth", block: "center" });
};
