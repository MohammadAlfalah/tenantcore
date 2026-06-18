import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { projectsApi } from "../api/projects";
import { tasksApi, type TaskInput } from "../api/tasks";
import { membersApi } from "../api/members";
import { apiErrorMessage } from "../api/client";
import {
  STATUS_LABELS,
  TASK_STATUSES,
  type Member,
  type ProjectDetail,
  type Task,
  type TaskStatus,
} from "../types";
import { useAuth } from "../auth/AuthContext";
import { canEdit, isAdmin } from "../lib/roles";
import { RoleGate } from "../components/RoleGate";
import { StatusBadge } from "../components/StatusBadge";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Modal,
  Select,
  Spinner,
  Textarea,
} from "../components/ui";

export function ProjectDetailPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const editable = canEdit(user?.role);

  const [project, setProject] = useState<ProjectDetail | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [taskModal, setTaskModal] = useState<{ open: boolean; task: Task | null }>({
    open: false,
    task: null,
  });
  const [editingProject, setEditingProject] = useState(false);

  const load = useCallback(async () => {
    try {
      const [proj, mem] = await Promise.all([projectsApi.get(id), membersApi.list()]);
      setProject(proj);
      setMembers(mem);
      setError("");
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to load project."));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  const tasksByStatus = useMemo(() => {
    const grouped: Record<TaskStatus, Task[]> = { Todo: [], InProgress: [], InReview: [], Done: [] };
    project?.tasks.forEach((t) => grouped[t.status].push(t));
    return grouped;
  }, [project]);

  const changeStatus = async (task: Task, status: TaskStatus) => {
    try {
      await tasksApi.updateStatus(task.id, status);
      await load();
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to update status."));
    }
  };

  const deleteTask = async (task: Task) => {
    if (!confirm(`Delete task "${task.title}"?`)) return;
    try {
      await tasksApi.remove(task.id);
      await load();
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to delete task."));
    }
  };

  const deleteProject = async () => {
    if (!project || !confirm(`Delete project "${project.name}" and all its tasks?`)) return;
    try {
      await projectsApi.remove(project.id);
      navigate("/", { replace: true });
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to delete project."));
    }
  };

  if (loading) {
    return (
      <div className="flex justify-center py-16">
        <Spinner className="h-8 w-8 text-brand-600" />
      </div>
    );
  }

  if (!project) {
    return (
      <div className="space-y-4">
        <Alert>{error || "Project not found."}</Alert>
        <Link to="/" className="text-sm font-medium text-brand-600">
          ← Back to dashboard
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <Link to="/" className="text-sm font-medium text-brand-600 hover:text-brand-700">
        ← Back to dashboard
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">{project.name}</h1>
          {project.description && <p className="mt-1 max-w-2xl text-slate-600">{project.description}</p>}
          <p className="mt-2 text-xs text-slate-400">Created by {project.createdByName}</p>
        </div>
        <div className="flex gap-2">
          <RoleGate allow={canEdit}>
            <Button onClick={() => setTaskModal({ open: true, task: null })}>+ Add task</Button>
          </RoleGate>
          <RoleGate allow={isAdmin}>
            <Button variant="secondary" onClick={() => setEditingProject(true)}>
              Edit
            </Button>
            <Button variant="danger" onClick={deleteProject}>
              Delete
            </Button>
          </RoleGate>
        </div>
      </div>

      {error && <Alert>{error}</Alert>}

      {project.tasks.length === 0 ? (
        <EmptyState
          title="No tasks yet"
          hint={editable ? "Add the first task to this project." : "Tasks will appear here once added."}
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {TASK_STATUSES.map((status) => (
            <div key={status} className="space-y-3">
              <div className="flex items-center justify-between px-1">
                <h2 className="text-sm font-semibold text-slate-700">{STATUS_LABELS[status]}</h2>
                <span className="text-xs text-slate-400">{tasksByStatus[status].length}</span>
              </div>
              <div className="space-y-3">
                {tasksByStatus[status].map((task) => (
                  <TaskCard
                    key={task.id}
                    task={task}
                    editable={editable}
                    onChangeStatus={(s) => changeStatus(task, s)}
                    onEdit={() => setTaskModal({ open: true, task })}
                    onDelete={() => deleteTask(task)}
                  />
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {taskModal.open && (
        <TaskFormModal
          task={taskModal.task}
          members={members}
          projectId={project.id}
          onClose={() => setTaskModal({ open: false, task: null })}
          onSaved={load}
        />
      )}

      {editingProject && (
        <EditProjectModal
          project={project}
          onClose={() => setEditingProject(false)}
          onSaved={load}
        />
      )}
    </div>
  );
}

function TaskCard({
  task,
  editable,
  onChangeStatus,
  onEdit,
  onDelete,
}: {
  task: Task;
  editable: boolean;
  onChangeStatus: (status: TaskStatus) => void;
  onEdit: () => void;
  onDelete: () => void;
}) {
  return (
    <Card className="p-4">
      <div className="flex items-start justify-between gap-2">
        <h3 className="text-sm font-semibold text-slate-900">{task.title}</h3>
        {!editable && <StatusBadge status={task.status} />}
      </div>
      {task.description && <p className="mt-1 text-xs text-slate-500">{task.description}</p>}

      <div className="mt-3 flex items-center gap-2 text-xs text-slate-500">
        <span className="flex h-6 w-6 items-center justify-center rounded-full bg-slate-200 text-[10px] font-semibold text-slate-600">
          {initials(task.assigneeName)}
        </span>
        <span>{task.assigneeName ?? "Unassigned"}</span>
      </div>

      {editable && (
        <div className="mt-3 flex items-center gap-2">
          <Select
            value={task.status}
            onChange={(e) => onChangeStatus(e.target.value as TaskStatus)}
            className="text-xs"
          >
            {TASK_STATUSES.map((s) => (
              <option key={s} value={s}>
                {STATUS_LABELS[s]}
              </option>
            ))}
          </Select>
          <Button variant="ghost" onClick={onEdit} className="px-2 py-1 text-xs">
            Edit
          </Button>
          <Button variant="ghost" onClick={onDelete} className="px-2 py-1 text-xs text-red-600">
            Delete
          </Button>
        </div>
      )}
    </Card>
  );
}

function TaskFormModal({
  task,
  members,
  projectId,
  onClose,
  onSaved,
}: {
  task: Task | null;
  members: Member[];
  projectId: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  // Mounted only while open, so these initializers run fresh each time the modal opens.
  const [form, setForm] = useState<TaskInput>(() => ({
    title: task?.title ?? "",
    description: task?.description ?? "",
    assigneeUserId: task?.assigneeUserId ?? null,
    status: task?.status ?? "Todo",
  }));
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError("");
    try {
      const payload: TaskInput = {
        ...form,
        description: form.description || null,
        assigneeUserId: form.assigneeUserId || null,
      };
      if (task) await tasksApi.update(task.id, payload);
      else await tasksApi.create(projectId, payload);
      onClose();
      onSaved();
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to save task."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal open title={task ? "Edit task" : "New task"} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        <Field label="Title">
          <Input
            value={form.title}
            onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
            required
            autoFocus
          />
        </Field>
        <Field label="Description">
          <Textarea
            rows={3}
            value={form.description ?? ""}
            onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
          />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Assignee">
            <Select
              value={form.assigneeUserId ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, assigneeUserId: e.target.value || null }))}
            >
              <option value="">Unassigned</option>
              {members.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.fullName}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Status">
            <Select
              value={form.status}
              onChange={(e) => setForm((f) => ({ ...f, status: e.target.value as TaskStatus }))}
            >
              {TASK_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {STATUS_LABELS[s]}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <Alert>{error}</Alert>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" loading={submitting}>
            {task ? "Save changes" : "Create task"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function EditProjectModal({
  project,
  onClose,
  onSaved,
}: {
  project: ProjectDetail;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState(project.name);
  const [description, setDescription] = useState(project.description ?? "");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError("");
    try {
      await projectsApi.update(project.id, { name, description: description || null });
      onClose();
      onSaved();
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to update project."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal open title="Edit project" onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        <Field label="Name">
          <Input value={name} onChange={(e) => setName(e.target.value)} required />
        </Field>
        <Field label="Description">
          <Textarea rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
        <Alert>{error}</Alert>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" loading={submitting}>
            Save changes
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function initials(name: string | null): string {
  if (!name) return "—";
  return name
    .split(" ")
    .map((p) => p[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();
}
