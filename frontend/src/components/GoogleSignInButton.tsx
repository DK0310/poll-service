import { GoogleLogin } from '@react-oauth/google';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { googleClientId } from '../auth/google';

// "Sign in with Google" button. On success it exchanges the Google ID token for our app JWT,
// then routes new Google users (no password yet) to set one, everyone else to their polls.
export function GoogleSignInButton() {
  const { loginWithGoogle } = useAuth();
  const navigate = useNavigate();

  if (!googleClientId) return null; // not configured → hide rather than render a broken button

  return (
    <div className="flex justify-center">
      <GoogleLogin
        locale="en"
        theme="filled_black"
        shape="pill"
        text="continue_with"
        onSuccess={async (credentialResponse) => {
          const idToken = credentialResponse.credential;
          if (!idToken) return;
          // Straight into the app — a password can be added later from Profile (no forced step).
          if (await loginWithGoogle(idToken)) navigate('/my-polls');
        }}
      />
    </div>
  );
}
