(() => {
    const FALLBACK_LOCALIZATION = {
        defaultCulture: "en-US",
        supportedCultures: ["en-US"],
        storageKey: "ufw.culture",
    };

    const BOOTSTRAP_STRINGS = {
        "en-US": {
            loading: "Loading application...",
            error: "An unexpected error occurred.",
            reload: "Reload",
            dismiss: "Dismiss",
        },
        "de-DE": {
            loading: "Anwendung wird geladen...",
            error: "Ein unerwarteter Fehler ist aufgetreten.",
            reload: "Neu laden",
            dismiss: "Schlie\u00dfen",
        },
    };

    async function loadLocalizationConfiguration() {
        try {
            const response = await fetch("appsettings.json", { cache: "no-store" });
            if (!response.ok) {
                return FALLBACK_LOCALIZATION;
            }

            const settings = await response.json();
            const localization = settings?.Localization;
            const supportedCultures = Array.isArray(localization?.SupportedCultures)
                ? localization.SupportedCultures.filter(value => typeof value === "string" && value.length > 0)
                : [];
            const defaultCulture = typeof localization?.DefaultCulture === "string"
                ? localization.DefaultCulture
                : FALLBACK_LOCALIZATION.defaultCulture;
            const storageKey = typeof localization?.StorageKey === "string"
                ? localization.StorageKey
                : FALLBACK_LOCALIZATION.storageKey;

            if (supportedCultures.length === 0 || !supportedCultures.includes(defaultCulture)) {
                return FALLBACK_LOCALIZATION;
            }

            return { defaultCulture, supportedCultures, storageKey };
        } catch {
            return FALLBACK_LOCALIZATION;
        }
    }

    function selectCulture(configuration) {
        try {
            const storedCulture = localStorage.getItem(configuration.storageKey);
            if (configuration.supportedCultures.includes(storedCulture)) {
                return storedCulture;
            }
        } catch {
            // Browser storage can be unavailable under restrictive privacy settings.
        }

        return configuration.defaultCulture;
    }

    function applyBootstrapStrings(culture) {
        document.documentElement.lang = culture;
        const strings = BOOTSTRAP_STRINGS[culture] ?? BOOTSTRAP_STRINGS["en-US"];

        const loading = document.getElementById("bootstrap-status-detail");
        if (loading) {
            loading.textContent = strings.loading;
        }

        const error = document.getElementById("blazor-error-message");
        if (error) {
            error.textContent = strings.error;
        }

        const reload = document.querySelector("#blazor-error-ui .reload");
        if (reload) {
            reload.textContent = strings.reload;
        }

        const dismiss = document.querySelector("#blazor-error-ui .dismiss");
        if (dismiss) {
            dismiss.setAttribute("aria-label", strings.dismiss);
        }
    }

    async function start() {
        const configuration = await loadLocalizationConfiguration();
        const culture = selectCulture(configuration);
        applyBootstrapStrings(culture);
        await Blazor.start({ applicationCulture: culture });
    }

    start().catch(error => console.error("Failed to start UFW Console.", error));
})();
