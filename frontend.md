Here are frontend design and implementation prompts you can use in your AI coding assistant (or UI generator) to build the frontend.
Prompt 1: The Merchant Dashboard & API Key Management
Copy and paste this prompt:
Context: I am building the frontend Merchant Dashboard for "PayFlow", a multi-tenant payment gateway.
Task: Design and implement the API Key management page.

    Create a global UI toggle in the navigation bar to switch between "Test Mode" and "Live Mode".
    Build a view to list active API keys. Test keys will start with pk_test_ and live keys will start with pk_live_.
    Implement a "Generate New Key" modal. When a key is successfully generated, display the full plaintext key to the user exactly once. Include a strong UI warning (e.g., a yellow alert box) stating: "Please copy this key now. For security reasons, it will never be shown again.".
    Ensure the UI visually disables or restricts actions if the tenant's status is returned as "Suspended" or "Closed".

Prompt 2: The Checkout / Payment Processing Component
Copy and paste this prompt:
Context: I am building the frontend client for creating payments via the PayFlow API. The backend requires strict idempotency and handles a specific state machine.
Task: Implement a resilient Payment Checkout component.

    When the user clicks "Pay", immediately generate a UUID (max 64 characters) to use as the Idempotency-Key. Store this temporarily in local state/storage.
    Send a POST /v1/payments request including this Idempotency-Key header.
    Implement strict error handling based on RFC 9457 application/problem+json standards.
        If the backend returns 409 payment_in_flight, show a "Payment processing..." spinner and poll or wait.
        If it returns 402 payment_declined, highlight the card input in red and prompt the user to try another payment method.
        If it returns 422 validation_error, display the specific validation messages under the corresponding input fields.
    Map the UI to the payment's state machine: visually indicate when a payment moves from Created to Authorised, and finally to Captured.

Prompt 3: Webhook Configuration Panel
Copy and paste this prompt:
Context: PayFlow relies on webhooks to notify merchants of asynchronous events (like payment.captured or refund.failed).
Task: Build the Webhook Endpoint configuration UI.

    Create a form to register a new webhook URL. The form must enforce frontend validation that rejects plaintext HTTP URLs; it must strictly require https://.
    Upon registering an endpoint, display the HMAC-SHA256 signing secret to the user. Note that this is the only time it will be shown.
    Add a "Rotate Secret" button next to existing webhook endpoints that calls POST /v1/webhook-endpoints/{id}/rotate-secret and displays the new secret.

Prompt 4: Transaction Details & Partial Refunds UI
Copy and paste this prompt:
Context: Merchants need a dashboard to view individual payment records and issue refunds.
Task: Create the Transaction Details view and Refund modal.

    Display the payment status. Only allow the "Issue Refund" button to be clickable if the payment status is exactly Settled.
    In the Refund modal, allow the user to enter a specific refund amount.
    Implement frontend validation ensuring the refund amount entered does not exceed the remaining refundable balance (Original Amount minus any existing successful refunds).
    Ensure the POST /v1/payments/{id}/refund request includes a newly generated Idempotency-Key header to prevent double-refunding if the network drops.

Prompt 5: Settlement & Reconciliation View
Copy and paste this prompt:
Context: The PayFlow backend runs a nightly job at 00:30 UTC to aggregate captured payments into Settlement Batches.
Task: Build a Reconciliation dashboard for accountants.

    Create a data table that fetches and displays SettlementBatch records using GET /v1/settlements/{id}.
    For each batch, display the Settlement Date, Total Gross Amount, Total Fees, and Net Amount.
    Allow the user to click into a batch to see the underlying PaymentId values that were included in that specific daily payout.