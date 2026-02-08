# Election Forecaster

A full-stack election forecasting web application featuring an interactive US map, state-specific pages with congressional district visualizations, and race forecasts for Senate, Governor, and House races.

## Features

- Interactive US map colored by race ratings (Solid D to Solid R)
- Click-to-navigate state detail pages
- Senate, Governor, and House race forecasts with probability bars
- District grid visualization for House races
- Hover tooltips showing candidate odds
- Responsive design

## Tech Stack

- **Backend:** ASP.NET Core 8 Web API
- **Frontend:** React 19 + TypeScript + Vite
- **Maps:** react-simple-maps with US Atlas TopoJSON
- **Data Fetching:** @tanstack/react-query + Axios

## Project Structure

```
ElectionForecaster/
├── ElectionForecaster.sln
├── ElectionForecaster.Api/              # ASP.NET Core Web API
│   ├── Controllers/
│   │   ├── StatesController.cs          # /api/states endpoints
│   │   ├── RacesController.cs           # /api/races endpoints
│   │   └── DistrictsController.cs       # /api/districts endpoints
│   ├── Program.cs                       # DI, CORS, Swagger config
│   └── Properties/launchSettings.json
├── ElectionForecaster.Core/             # Domain Layer
│   ├── Models/
│   │   ├── State.cs
│   │   ├── Race.cs
│   │   ├── District.cs
│   │   ├── Candidate.cs
│   │   └── Forecast.cs
│   ├── Enums/
│   │   ├── RaceType.cs                  # Senate, Governor, House
│   │   ├── Party.cs                     # Democrat, Republican, etc.
│   │   └── RaceRating.cs                # SolidDem to SolidRep
│   └── Interfaces/
│       ├── IStateService.cs
│       ├── IRaceService.cs
│       └── IDistrictService.cs
├── ElectionForecaster.Infrastructure/   # Data Layer
│   ├── Data/
│   │   └── MockDataProvider.cs          # Generates all 50 states mock data
│   └── Services/
│       ├── StateService.cs
│       ├── RaceService.cs
│       └── DistrictService.cs
└── election-forecaster-client/          # React Frontend
    └── src/
        ├── types/index.ts               # TypeScript interfaces
        ├── services/api.ts              # Axios API client
        ├── components/
        │   ├── maps/
        │   │   ├── USMap.tsx            # Interactive US map
        │   │   ├── StateMap.tsx         # District grid
        │   │   └── MapLegend.tsx        # Rating color legend
        │   └── races/
        │       └── RaceCard.tsx         # Candidate probability bars
        └── pages/
            ├── HomePage.tsx             # US map + stats
            └── StatePage.tsx            # State detail page
```

## Architecture

The backend follows **Clean Architecture** with three layers:

1. **Core** - Domain models, enums, and service interfaces (no dependencies)
2. **Infrastructure** - Service implementations and data access
3. **Api** - Controllers, DI configuration, and HTTP pipeline

The frontend uses a component-based architecture with:
- **Pages** - Route-level components
- **Components** - Reusable UI elements
- **Services** - API communication layer
- **Types** - Shared TypeScript definitions

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/states` | All states with summary info |
| GET | `/api/states/{id}` | State detail with all races |
| GET | `/api/states/{id}/races` | All races for a state |
| GET | `/api/states/{id}/districts` | Congressional districts |
| GET | `/api/races` | All races (filterable by type) |
| GET | `/api/races/{id}` | Single race detail |
| GET | `/api/districts/{id}` | Single district detail |

## Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- npm

### Running the Backend

```bash
cd ElectionForecaster.Api
dotnet run
```

The API will be available at http://localhost:5000 with Swagger UI at http://localhost:5000/swagger

### Running the Frontend

**Note:** Due to the `#` character in "C# Projects", the frontend should be run from a path without special characters.

```bash
# Copy to a path without # (if not already done)
cp -r election-forecaster-client ~/Desktop/election-forecaster-client

# Install dependencies
cd ~/Desktop/election-forecaster-client
npm install

# Start dev server
npm run dev
```

The frontend will be available at http://localhost:5173 (or next available port)

## Mock Data

The `MockDataProvider` generates realistic election data including:

- All 50 US states with accurate electoral votes and congressional district counts
- Real 2024 candidate names for Senate and Governor races
- Procedurally generated House candidates
- Race ratings (Solid D, Likely D, Lean D, Tossup, Lean R, Likely R, Solid R)
- Win probabilities based on race ratings

## Race Rating Color Scheme

| Rating | Color | Hex |
|--------|-------|-----|
| Solid Dem | Dark Blue | #0015BC |
| Likely Dem | Blue | #3355DD |
| Lean Dem | Light Blue | #7799EE |
| Tossup | Purple | #9966CC |
| Lean Rep | Light Red | #EE7777 |
| Likely Rep | Red | #DD3333 |
| Solid Rep | Dark Red | #BC0000 |

## Known Issues

- **Path with `#` character**: Vite has issues with paths containing `#` (like "C# Projects"). Run the frontend from a different location if you encounter this.
- **Node.js version**: Vite 5.x requires Node.js 20+. If using an older version, you may need to downgrade Vite.

## Deployment

### Option 1: Free Tier Stack (Recommended)

**Backend: Render (Free)**

1. Create a [Render](https://render.com) account
2. Click "New" → "Web Service"
3. Connect your GitHub repository
4. Configure:
   - **Name:** `election-forecaster-api`
   - **Runtime:** Docker
   - **Plan:** Free
5. Add environment variable:
   - `Cors__AllowedOrigins__0` = `https://your-frontend-domain.vercel.app`
6. Deploy

Your API will be available at `https://election-forecaster-api.onrender.com`

> **Note:** Free tier spins down after 15 minutes of inactivity. First request after sleep takes ~30 seconds.

**Frontend: Vercel (Free)**

1. Create a [Vercel](https://vercel.com) account
2. Click "Add New" → "Project"
3. Import your GitHub repository
4. Configure:
   - **Root Directory:** `election-forecaster-client`
   - **Framework Preset:** Vite
5. Add environment variable:
   - `VITE_API_URL` = `https://election-forecaster-api.onrender.com/api`
6. Deploy

Your frontend will be available at `https://your-project.vercel.app`

**Domain: Cloudflare (~$10/year)**

1. Purchase domain at [Cloudflare Registrar](https://www.cloudflare.com/products/registrar/)
2. In Vercel: Settings → Domains → Add your domain
3. In Render: Settings → Custom Domain → Add your API subdomain (e.g., `api.yourdomain.com`)
4. Update the `VITE_API_URL` in Vercel to use your API subdomain
5. Update `Cors__AllowedOrigins__0` in Render to use your frontend domain

### Environment Variables Reference

**Backend (Render)**
| Variable | Description | Example |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |
| `Cors__AllowedOrigins__0` | Allowed frontend origin | `https://yourdomain.com` |

**Frontend (Vercel)**
| Variable | Description | Example |
|----------|-------------|---------|
| `VITE_API_URL` | Backend API URL | `https://api.yourdomain.com/api` |

### Security Features

- **Rate Limiting**: 100 requests per minute per client
- **CORS**: Only configured origins can make browser requests
- **Swagger disabled in production**: API documentation only available in development

## License

MIT
