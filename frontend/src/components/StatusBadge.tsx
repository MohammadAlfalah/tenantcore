import { Badge } from "./ui";
import { STATUS_LABELS, type TaskStatus } from "../types";

const STATUS_CLASSES: Record<TaskStatus, string> = {
  Todo: "bg-slate-200 text-slate-700",
  InProgress: "bg-blue-100 text-blue-700",
  InReview: "bg-amber-100 text-amber-700",
  Done: "bg-emerald-100 text-emerald-700",
};

export function StatusBadge({ status }: { status: TaskStatus }) {
  return <Badge className={STATUS_CLASSES[status]}>{STATUS_LABELS[status]}</Badge>;
}
