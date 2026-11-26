export default function Spinner({ size = 'md', label = 'Loading...' }: { size?: 'sm' | 'md' | 'lg'; label?: string }) {
  const sizeClasses = {
    sm: 'h-4 w-4 border-2',
    md: 'h-8 w-8 border-2',
    lg: 'h-12 w-12 border-4',
  };
  return (
    <div className="flex flex-col items-center gap-3">
      <div
        className={`${sizeClasses[size]} animate-spin rounded-full border-indigo-600 border-t-transparent`}
        role="status"
        aria-label={label}
      />
      {label && <span className="text-sm text-gray-500">{label}</span>}
    </div>
  );
}
