# PayFlow Frontend

A React + TypeScript frontend for the PayFlow payment processing platform.

## Tech Stack

- **React 18** with TypeScript
- **Vite** - Build tool and dev server
- **Tailwind CSS v4** - Utility-first CSS framework
- **React Router v6** - Client-side routing
- **Lucide React** - Icon library

## Project Structure

```
frontend/
├── src/
│   ├── api/
│   │   └── client.ts           # API client for all endpoints
│   ├── components/
│   │   └── Layout.tsx          # Main layout with navigation
│   ├── contexts/
│   │   └── AuthContext.tsx     # Authentication context
│   ├── pages/
│   │   ├── LoginPage.tsx       # API key login
│   │   ├── DashboardPage.tsx   # Overview dashboard
│   │   ├── ApiKeysPage.tsx     # API key management
│   │   ├── PaymentsPage.tsx    # Payment list & creation
│   │   ├── PaymentDetailsPage.tsx # Payment details & refunds
│   │   ├── WebhooksPage.tsx    # Webhook configuration
│   │   └── SettlementsPage.tsx # Settlement batches
│   ├── types/
│   │   └── index.ts            # TypeScript type definitions
│   ├── App.tsx                 # Main app with routing
│   └── index.css               # Global styles with Tailwind
├── index.html
├── package.json
├── postcss.config.js
├── tsconfig.json
└── vite.config.ts
```

## Pages

### Login Page (`/login`)
- API key input with validation
- Supports `pk_test_` and `pk_live_` prefixes
- Persistent session via localStorage

### Dashboard (`/`)
- Overview statistics
- Quick action cards
- Recent activity feed
- Mode indicator (Test/Live)

### API Keys (`/api-keys`)
- List API keys filtered by mode
- Generate new key modal
- One-time key display with security warning
- Revoke key functionality

### Payments (`/payments`)
- Payment list with status indicators
- Create payment modal with idempotency
- Real-time status tracking
- RFC 9457 error handling

### Payment Details (`/payments/:id`)
- Full payment information
- Capture, Cancel, and Refund actions
- Refund modal with amount validation

### Webhooks (`/webhooks`)
- Webhook endpoint list
- Create endpoint with HTTPS validation
- Event type selection
- Rotate secret functionality

### Settlements (`/settlements`)
- Settlement batch table
- Date range filtering
- Detailed breakdown

## Getting Started

### Prerequisites
- Node.js 18+
- npm or yarn

### Installation
```bash
cd frontend
npm install
```

### Development
```bash
npm run dev
```
Opens at http://localhost:5173

### Production Build
```bash
npm run build
```
Output in `dist/` directory

## Environment Variables

Create a `.env` file:
```
VITE_API_URL=http://localhost:5062
```

## API Integration

The frontend connects to the PayFlow backend API. All requests include:
- Bearer token authentication (API key)
- Automatic idempotency key generation for payments
- RFC 9457 error handling

## Features

- ✅ Test/Live mode toggle
- ✅ API key generation with one-time display
- ✅ HTTPS enforcement for webhook URLs
- ✅ Idempotency key handling
- ✅ Payment state machine visualization
- ✅ Partial refund support
- ✅ Webhook secret rotation
- ✅ Settlement batch filtering
- ✅ Responsive design
- ✅ Error handling with problem details
- ✅ Loading states and spinners

## License
MIT License