import { TransactionStatus } from '@/types/api';

const statusConfig: Record<TransactionStatus, { label: string; classes: string; dot: string }> = {
  Pending:   { label: 'Pending',   classes: 'bg-yellow-100 text-yellow-800', dot: 'bg-yellow-400' },
  Succeeded: { label: 'Succeeded', classes: 'bg-green-100  text-green-800',  dot: 'bg-green-500'  },
  Failed:    { label: 'Failed',    classes: 'bg-red-100    text-red-800',    dot: 'bg-red-500'    },
};

export default function StatusBadge({ status }: { status: TransactionStatus }) {
  const config = statusConfig[status] ?? statusConfig.Pending;
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-medium ${config.classes}`}>
      <span className={`h-2 w-2 rounded-full ${config.dot}`} />
      {config.label}
    </span>
  );
}
