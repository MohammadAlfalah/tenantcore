import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { projectsApi } from "../api/projects";
import { apiErrorMessage } from "../api/client";
import type { ProjectSummary } from "../types";
import { isAdmin } from "../lib/roles";
import { RoleGate } from "../components/RoleGate";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Modal,
  Spinner,
  Textarea,
} from "../components/ui";

export function DashboardPage() {
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [modalOpen, setModalOpen] = useState(false);

  const load = async () => {
    try {
      setProjects(await projectsApi.list());
      setError("");
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to load projects."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Projects</h1>
          <p className="text-sm text-slate-500">Everything your team is working on.</p>
        </div>
        <RoleGate allow={isAdmin}>
          <Button onClick={() => setModalOpen(true)}>+ New project</Button>
        </RoleGate>
      </div>

      {error && <Alert>{error}</Alert>}

      {loading ? (
        <div className="flex justify-center py-16">
          <Spinner className="h-8 w-8 text-brand-600" />
        </div>
      ) : projects.length === 0 ? (
        <EmptyState
          title="No projects yet"
          hint="Admins can create the first project to get started."
        />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {projects.map((p) => (
            <Link key={p.id} to={`/projects/${p.id}`}>
              <Card className="h-full p-5 transition hover:shadow-md">
                <h3 className="font-semibold text-slate-900">{p.name}</h3>
                <p className="mt-1 line-clamp-2 min-h-[2.5rem] text-sm text-slate-500">
                  {p.description || "No description"}
                </p>
                <div className="mt-4 flex items-center justify-between text-xs text-slate-500">
                  <span>
                    {p.taskCount} task{p.taskCount === 1 ? "" : "s"}
                  </span>
                  <span className="rounded-full bg-brand-50 px-2 py-0.5 font-medium text-brand-700">
                    {p.openTaskCount} open
                  </span>
                </div>
                <p className="mt-3 text-xs text-slate-400">Created by {p.createdByName}</p>
              </Card>
            </Link>
          ))}
        </div>
      )}

      <CreateProjectModal open={modalOpen} onClose={() => setModalOpen(false)} onCreated={load} />
    </div>
  );
}

function CreateProjectModal({
  open,
  onClose,
  onCreated,
}: {
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
}) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const reset = () => {
    setName("");
    setDescription("");
    setError("");
  };

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError("");
    try {
      await projectsApi.create({ name, description: description || null });
      reset();
      onClose();
      onCreated();
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to create project."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal open={open} title="New project" onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        <Field label="Name">
          <Input value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
        </Field>
        <Field label="Description">
          <Textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            placeholder="Optional"
          />
        </Field>
        <Alert>{error}</Alert>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" loading={submitting}>
            Create
          </Button>
        </div>
      </form>
    </Modal>
  );
}
