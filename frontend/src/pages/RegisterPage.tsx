import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { apiErrorMessage } from "../api/client";
import { Alert, Button, Field, Input } from "../components/ui";
import { AuthShell } from "./LoginPage";

export function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ tenantName: "", fullName: "", email: "", password: "" });
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const update = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      await register(form);
      navigate("/", { replace: true });
    } catch (err) {
      setError(apiErrorMessage(err, "Unable to create workspace."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthShell title="Create your workspace" subtitle="Sign up creates a new, isolated tenant">
      <form onSubmit={onSubmit} className="space-y-4">
        <Field label="Company name">
          <Input
            value={form.tenantName}
            onChange={update("tenantName")}
            placeholder="Acme Inc"
            required
            autoFocus
          />
        </Field>
        <Field label="Your name">
          <Input value={form.fullName} onChange={update("fullName")} placeholder="Ada Lovelace" required />
        </Field>
        <Field label="Email">
          <Input
            type="email"
            value={form.email}
            onChange={update("email")}
            placeholder="you@acme.com"
            required
          />
        </Field>
        <Field label="Password">
          <Input
            type="password"
            value={form.password}
            onChange={update("password")}
            placeholder="At least 8 characters"
            minLength={8}
            required
          />
        </Field>
        <p className="text-xs text-slate-500">
          You'll be the first <span className="font-medium text-slate-700">Admin</span> of this
          workspace.
        </p>
        <Alert>{error}</Alert>
        <Button type="submit" loading={submitting} className="w-full">
          Create workspace
        </Button>
      </form>
      <p className="mt-6 text-center text-sm text-slate-500">
        Already have an account?{" "}
        <Link to="/login" className="font-medium text-brand-600 hover:text-brand-700">
          Sign in
        </Link>
      </p>
    </AuthShell>
  );
}
