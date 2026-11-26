import { Product } from '@/types/api';

interface PaymentSummaryProps {
  product: Product | null;
  customerName?: string;
  usingSavedCard?: boolean;
  maskedCardNo?: string;
}

export default function PaymentSummary({ product, customerName, usingSavedCard, maskedCardNo }: PaymentSummaryProps) {
  if (!product) {
    return (
      <div className="rounded-xl border border-dashed border-gray-300 bg-gray-50 p-6 text-center text-sm text-gray-400">
        Select a product to see payment summary
      </div>
    );
  }

  const badgeColor =
    product.paymentType === 'Recurring'
      ? 'bg-purple-100 text-purple-800'
      : 'bg-blue-100 text-blue-800';

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
      <h3 className="text-base font-semibold text-gray-900 mb-4">Order Summary</h3>
      <dl className="space-y-3 text-sm">
        {customerName && (
          <div className="flex justify-between">
            <dt className="text-gray-500">Customer</dt>
            <dd className="font-medium text-gray-900">{customerName}</dd>
          </div>
        )}
        <div className="flex justify-between">
          <dt className="text-gray-500">Product</dt>
          <dd className="font-medium text-gray-900">{product.name}</dd>
        </div>
        <div className="flex justify-between">
          <dt className="text-gray-500">Type</dt>
          <dd>
            <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${badgeColor}`}>
              {product.paymentType}
            </span>
          </dd>
        </div>
        <div className="flex justify-between">
          <dt className="text-gray-500">Currency</dt>
          <dd className="font-medium text-gray-900">{product.currency}</dd>
        </div>
        {usingSavedCard && maskedCardNo && (
          <div className="flex justify-between">
            <dt className="text-gray-500">Saved Card</dt>
            <dd className="font-medium text-gray-900 font-mono">{maskedCardNo}</dd>
          </div>
        )}
        <div className="border-t border-gray-200 pt-3 flex justify-between">
          <dt className="font-semibold text-gray-900">Total</dt>
          <dd className="font-bold text-lg text-indigo-600">
            {product.currency} {Number(product.price).toFixed(2)}
          </dd>
        </div>
      </dl>
    </div>
  );
}
