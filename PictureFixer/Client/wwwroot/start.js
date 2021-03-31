(async function () {
    // Only enable CSS animations *after* the application becomes interactive. Otherwise loading is annoying.
    document.documentElement.classList.add('disable-animations');
    await Blazor.start();
    setTimeout(() => document.documentElement.classList.remove('disable-animations'), 1000);
})();
