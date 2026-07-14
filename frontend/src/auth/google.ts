// The Google OAuth Web client ID (public, embedded in the bundle). When unset, social login
// is simply hidden — the app still works with email + password.
export const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;
