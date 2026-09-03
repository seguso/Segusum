window.segusumTranslatorScrollTo = function (id) {
    const element = document.getElementById(id);
    if (element) element.scrollIntoView({ behavior: "smooth", block: "center" });
};
