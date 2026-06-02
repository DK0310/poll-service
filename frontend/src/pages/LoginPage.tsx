import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export function LoginPage() {
  const { login, loading, error } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (await login(email, password)) navigate('/my-polls');
  };

  return (
    <div className="page">
      <h1>Log in</h1>
      <form onSubmit={handleSubmit} className="poll-form">
        <div className="form-group">
          <label htmlFor="email">Email</label>
          <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)}
            disabled={loading} required />
        </div>
        <div className="form-group">
          <label htmlFor="password">Password</label>
          <input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)}
            disabled={loading} required />
        </div>
        <button type="submit" className="btn-create" disabled={loading}>Log in</button>
        {error && <p className="error">{error}</p>}
      </form>
      <p>No account? <Link to="/register">Register</Link></p>
    </div>
  );
}
