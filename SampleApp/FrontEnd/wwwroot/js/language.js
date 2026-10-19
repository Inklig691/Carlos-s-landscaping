window.carlosLanguage = {
    get: () => window.localStorage.getItem("preferredLanguage"),
    set: (code) => {
        window.localStorage.setItem("preferredLanguage", code);
        document.documentElement.lang = code === "en-US" ? "en" : "es";
    }
};