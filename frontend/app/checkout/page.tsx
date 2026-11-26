'use client';

import { useEffect, useState, useCallback, Suspense } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import { Customer, CustomerCard, Product } from '@/types/api';
import { fetchCustomers, fetchProducts, fetchCustomerCards, processCheckout } from '@/services/api';
import Select from '@/components/Select';
import PaymentSummary from '@/components/PaymentSummary';
import Spinner from '@/components/Spinner';

function CheckoutContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialCustomerId = searchParams.get('customerId') ?? '';
  const initialProductId = searchParams.get('productId') ?? '';

  // Data
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [customerCards, setCustomerCards] = useState<CustomerCard[]>([]);

  // Selections
  const [selectedCustomerId, setSelectedCustomerId] = useState<string>(initialCustomerId);
  const [selectedProductId, setSelectedProductId] = useState<string>(initialProductId);
  const [selectedCardId, setSelectedCardId] = useState<string>('');
  const [useNewCard, setUseNewCard] = useState(false);

  // State
  const [loading, setLoading] = useState(false);
  const [dataLoading, setDataLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [idempotencyKey, setIdempotencyKey] = useState<string>('');

  // Generate idempotency key once on mount
  useEffect(() => {
    setIdempotencyKey(crypto.randomUUID());
  }, []);

  // Load customers and products on mount
  useEffect(() => {
    setDataLoading(true);
    Promise.all([fetchCustomers(), fetchProducts()])
      .then(([c, p]) => {
        setCustomers(c);
        setProducts(p);
      })
      .catch(() => setError('Failed to load data. Is the backend running?'))
      .finally(() => setDataLoading(false));
  }, []);

  // Load saved cards when customer changes
  useEffect(() => {
    setSelectedCardId('');
    setUseNewCard(false);
    if (!selectedCustomerId) {
      setCustomerCards([]);
      return;
    }
    fetchCustomerCards(Number(selectedCustomerId))
      .then(setCustomerCards)
      .catch(() => setCustomerCards([]));
  }, [selectedCustomerId]);

  const selectedCustomer = customers.find((c) => String(c.id) === selectedCustomerId);
  const selectedProduct = products.find((p) => String(p.id) === selectedProductId);

  // Saved cards can be used for any product (OneTime or Recurring) when customer has saved cards
  const showSavedCardOption = !!selectedProduct && customerCards.length > 0;
  const usingSavedCard = showSavedCardOption && !!selectedCardId && !useNewCard;
  const selectedCard = customerCards.find((c) => String(c.id) === selectedCardId);

  const isFormValid = !!selectedCustomerId && !!selectedProductId;

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isFormValid || loading) return;

    setLoading(true);
    setError(null);

    try {
      const customerCardId = usingSavedCard && selectedCardId ? Number(selectedCardId) : null;
      const result = await processCheckout(
        idempotencyKey,
        {
          customerId: Number(selectedCustomerId),
          productId: Number(selectedProductId),
          customerCardId,
        }
      );

      if (result.redirectUrl) {
        // Redirect to 2C2P hosted payment page or 3DS URL
        window.location.href = result.redirectUrl;
      } else {
        // No redirect (saved card completed inline)
        router.push(`/result?invoice=${encodeURIComponent(result.invoiceNo)}`);
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Checkout failed. Please try again.';
      setError(msg);
      // Regenerate idempotency key so user can retry
      setIdempotencyKey(crypto.randomUUID());
    } finally {
      setLoading(false);
    }
  }, [isFormValid, loading, usingSavedCard, selectedCardId, selectedCustomerId, selectedProductId, idempotencyKey, router]);

  if (dataLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Spinner size="lg" label="Loading checkout..." />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-indigo-50 to-white py-12 px-4">
      <div className="mx-auto max-w-4xl">
        {/* Header */}
        <div className="text-center mb-10">
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight">Rezerv Checkout</h1>
          <p className="mt-2 text-sm text-gray-500">Powered by 2C2P Payment Gateway</p>
        </div>

        <div className="grid grid-cols-1 gap-8 lg:grid-cols-5">
          {/* Form */}
          <div className="lg:col-span-3">
            <div className="rounded-2xl bg-white shadow-sm border border-gray-200 p-6 md:p-8">
              <h2 className="text-lg font-semibold text-gray-900 mb-6">Payment Details</h2>

              <form onSubmit={handleSubmit} className="space-y-5">
                {/* Customer */}
                <Select
                  id="customer"
                  label="Customer"
                  value={selectedCustomerId}
                  onChange={setSelectedCustomerId}
                  options={customers.map((c) => ({
                    value: String(c.id),
                    label: `${c.firstName} ${c.lastName} (${c.email})`,
                  }))}
                  placeholder="Select a customer"
                  required
                />

                {/* Product */}
                <Select
                  id="product"
                  label="Product"
                  value={selectedProductId}
                  onChange={(v) => {
                    setSelectedProductId(v);
                    setSelectedCardId('');
                    setUseNewCard(false);
                  }}
                  options={products.map((p) => ({
                    value: String(p.id),
                    label: `${p.name} — ${p.currency} ${Number(p.price).toFixed(2)} (${p.paymentType})`,
                  }))}
                  placeholder="Select a product"
                  required
                />

                {/* Saved Card (shown whenever customer has saved cards) */}
                {showSavedCardOption && !useNewCard && (
                  <div className="space-y-3">
                    <Select
                      id="savedCard"
                      label="Pay with Saved Card"
                      value={selectedCardId}
                      onChange={setSelectedCardId}
                      options={customerCards.map((cc) => ({
                        value: String(cc.id),
                        label: cc.maskedCardNo,
                      }))}
                      placeholder="Choose a saved card or use new payment method"
                    />
                    <button
                      type="button"
                      onClick={() => { setSelectedCardId(''); setUseNewCard(true); }}
                      className="text-sm text-indigo-600 hover:text-indigo-800 underline"
                    >
                      Pay with a new payment method instead
                    </button>
                  </div>
                )}

                {showSavedCardOption && useNewCard && (
                  <div className="rounded-lg bg-indigo-50 border border-indigo-200 px-4 py-3 text-sm text-indigo-700 flex items-center justify-between">
                    <span>
                      Using new payment method
                      {selectedProduct?.paymentType === 'Recurring' ? ' (card can be saved for future use)' : ''}
                    </span>
                    <button
                      type="button"
                      onClick={() => setUseNewCard(false)}
                      className="ml-3 text-indigo-600 hover:text-indigo-800 underline text-xs"
                    >
                      Use saved card
                    </button>
                  </div>
                )}

                {/* Idempotency key display (dev-friendly) */}
                <div className="rounded-lg bg-gray-50 border border-gray-200 px-4 py-3">
                  <p className="text-xs text-gray-500">
                    <span className="font-medium">Idempotency Key:</span>{' '}
                    <code className="font-mono text-gray-700 break-all">{idempotencyKey}</code>
                  </p>
                </div>

                {error && (
                  <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
                    {error}
                  </div>
                )}

                <button
                  type="submit"
                  disabled={!isFormValid || loading}
                  className="w-full rounded-lg bg-indigo-600 py-3 px-4 text-sm font-semibold text-white shadow-sm hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:bg-indigo-300 transition-colors duration-150"
                >
                  {loading ? (
                    <span className="flex items-center justify-center gap-2">
                      <svg className="h-4 w-4 animate-spin text-white" viewBox="0 0 24 24" fill="none">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z" />
                      </svg>
                      Processing...
                    </span>
                  ) : (
                    `Pay ${selectedProduct ? `${selectedProduct.currency} ${Number(selectedProduct.price).toFixed(2)}` : 'Now'}`
                  )}
                </button>
              </form>
            </div>

            {/* Security notice */}
            <div className="mt-4 flex items-center justify-center gap-2 text-xs text-gray-400">
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              </svg>
              Secured by 2C2P · Sandbox Mode
            </div>
          </div>

          {/* Summary */}
          <div className="lg:col-span-2 space-y-4">
            <PaymentSummary
              product={selectedProduct ?? null}
              customerName={selectedCustomer ? `${selectedCustomer.firstName} ${selectedCustomer.lastName}` : undefined}
              usingSavedCard={usingSavedCard}
              maskedCardNo={selectedCard?.maskedCardNo}
            />

            {selectedProduct && (
              <div className="rounded-xl border border-gray-200 bg-white p-4 text-xs text-gray-500 space-y-1 shadow-sm">
                <p className="font-medium text-gray-700 mb-1">Payment Mode</p>
                {usingSavedCard ? (
                  <p>Saved card via 2C2P Direct API (no hosted page)</p>
                ) : selectedProduct.paymentType === 'Recurring' ? (
                  <p>Recurring: Hosted page + card tokenization enabled</p>
                ) : (
                  <p>One-time: Hosted page, all payment methods</p>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export default function CheckoutPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center">
          <Spinner size="lg" label="Loading checkout..." />
        </div>
      }
    >
      <CheckoutContent />
    </Suspense>
  );
}
