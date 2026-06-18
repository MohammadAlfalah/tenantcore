import { useEffect, useState, type FormEvent } from "react";
import { membersApi } from "../api/members";
import { apiErrorMessage } from "../api/client";
import type { Member, UserRole } from "../types";
import { useAuth } from "../auth/AuthContext";
import { ROLE_BADGE_CLASSES, ROLE_LABELS } from "../lib/roles";
import {
  Alert,
  Badge,
  Button,
  Card,
  Field,
  Input,
  Modal,
  Select,
  Spinner,
} from "../components/ui";

const ROLES: UserRole[] = ["Admin", "Member", "Viewer"];

export function MembersPage() {
  const { user } = useAuth();
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [modal, setModal] = useState<{ open: boolean; member: Member | null }>({
    open: false,
    member: null,
  });

  const load = async () => {
    try {
      setMembers(await membersApi.list());
      setError("");
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to load members."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const remove = async (member: Member) => {
    if (!confirm(`Remove ${member.fullName}?`)) return;
    try {
      await membersApi.remove(member.id);
      await load();
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to remove member."));
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Members</h1>
          <p className="text-sm text-slate-500">Manage who can access this workspace.</p>
        </div>
        <Button onClick={() => setModal({ open: true, member: null })}>+ Invite member</Button>
      </div>

      {error && <Alert>{error}</Alert>}

      {loading ? (
        <div className="flex justify-center py-16">
          <Spinner className="h-8 w-8 text-brand-600" />
        </div>
      ) : (
        <Card className="overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-5 py-3 font-medium">Name</th>
                <th className="px-5 py-3 font-medium">Email</th>
                <th className="px-5 py-3 font-medium">Role</th>
                <th className="px-5 py-3 text-right font-medium">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {members.map((m) => {
                const isSelf = m.id === user?.id;
                return (
                  <tr key={m.id} className="hover:bg-slate-50">
                    <td className="px-5 py-3 font-medium text-slate-800">
                      {m.fullName}
                      {isSelf && <span className="ml-2 text-xs text-slate-400">(you)</span>}
                    </td>
                    <td className="px-5 py-3 text-slate-600">{m.email}</td>
                    <td className="px-5 py-3">
                      <Badge className={ROLE_BADGE_CLASSES[m.role]}>{ROLE_LABELS[m.role]}</Badge>
                    </td>
                    <td className="px-5 py-3 text-right">
                      <div className="flex justify-end gap-2">
                        <Button
                          variant="ghost"
                          className="px-2 py-1 text-xs"
                          onClick={() => setModal({ open: true, member: m })}
                        >
                          Edit
                        </Button>
                        {!isSelf && (
                          <Button
                            variant="ghost"
                            className="px-2 py-1 text-xs text-red-600"
                            onClick={() => remove(m)}
                          >
                            Remove
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </Card>
      )}

      {modal.open && (
        <MemberFormModal
          member={modal.member}
          onClose={() => setModal({ open: false, member: null })}
          onSaved={load}
        />
      )}
    </div>
  );
}

function MemberFormModal({
  member,
  onClose,
  onSaved,
}: {
  member: Member | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const editing = Boolean(member);
  // Mounted only while open, so this initializer runs fresh each time the modal opens.
  const [form, setForm] = useState(() => ({
    fullName: member?.fullName ?? "",
    email: member?.email ?? "",
    password: "",
    role: member?.role ?? ("Member" as UserRole),
  }));
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError("");
    try {
      if (member) {
        await membersApi.update(member.id, { fullName: form.fullName, role: form.role });
      } else {
        await membersApi.create({
          fullName: form.fullName,
          email: form.email,
          password: form.password,
          role: form.role,
        });
      }
      onClose();
      onSaved();
    } catch (err) {
      setError(apiErrorMessage(err, "Failed to save member."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal open title={editing ? "Edit member" : "Invite member"} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        <Field label="Full name">
          <Input
            value={form.fullName}
            onChange={(e) => setForm((f) => ({ ...f, fullName: e.target.value }))}
            required
            autoFocus
          />
        </Field>
        {!editing && (
          <>
            <Field label="Email">
              <Input
                type="email"
                value={form.email}
                onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                required
              />
            </Field>
            <Field label="Temporary password">
              <Input
                type="password"
                value={form.password}
                onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
                minLength={8}
                required
              />
            </Field>
          </>
        )}
        <Field label="Role">
          <Select
            value={form.role}
            onChange={(e) => setForm((f) => ({ ...f, role: e.target.value as UserRole }))}
          >
            {ROLES.map((r) => (
              <option key={r} value={r}>
                {ROLE_LABELS[r]}
              </option>
            ))}
          </Select>
        </Field>
        <Alert>{error}</Alert>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" loading={submitting}>
            {editing ? "Save changes" : "Create member"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
