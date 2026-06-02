import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export function RegisterPage() {
  const { register, loading, error } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (await register(email, password)) navigate('/my-polls');
  };

  return (
    <div className="page">
      <h1>Create an account</h1>
      <form onSubmit={handleSubmit} className="poll-form">
        <div className="form-group">
          <label htmlFor="email">Email</label>
          <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)}
            disabled={loading} required />
        </div>
        <div className="form-group">
          <label htmlFor="password">Password (min 6 characters)</label>
          <input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)}
            minLength={6} disabled={loading} required />
        </div>
        <button type="submit" className="btn-create" disabled={loading}>Register</button>
        {error && <p className="error">{error}</p>}
      </form>
      <p>Already have an account? <Link to="/login">Log in</Link></p>
    </div>
  );
}
