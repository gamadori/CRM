window.crmTheme = (() => {
    const storageKey = "crm-color-scheme";

    function preferredDarkTheme() {
        const stored = window.localStorage.getItem(storageKey);
        if (stored === "dark" || stored === "light") {
            return stored === "dark";
        }

        return window.matchMedia?.("(prefers-color-scheme: dark)").matches ?? false;
    }

    function apply(isDark) {
        const scheme = isDark ? "dark" : "light";
        document.documentElement.dataset.bsTheme = scheme;
        document.documentElement.style.colorScheme = scheme;
        document.body?.classList.toggle("dark-theme", isDark);
    }

    return {
        get: () => preferredDarkTheme(),
        apply,
        set: isDark => {
            window.localStorage.setItem(storageKey, isDark ? "dark" : "light");
            apply(isDark);
        }
    };
})();
