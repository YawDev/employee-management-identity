# Employee Management Identity Microservice

**A .NET-based authentication microservice using ASP.NET Core Identity for an Employee Management tool. Handles user registration, login, and role-based access, designed to integrate with a Next.js frontend and other backend microservices.**

---

## Table of Contents

1. [Overview](#overview)
2. [Tech Stack](#tech-stack)
3. [User Schema](#user-schema)
4. [Roles](#roles)
5. [Endpoints](#endpoints)
6. [Integration](#integration)

---

## Overview

This microservice manages authentication and authorization for the Employee Management system. It leverages **ASP.NET Core Identity** for user and role management.

It provides secure login, registration, and role-based access to other microservices and frontend applications.

---

## Tech Stack

- **Backend**: .NET 9 / ASP.NET Core
- **Database**: PostgreSQL (Npgsql) via EF Core
- **Identity**: ASP.NET Core Identity
- **Frontend Integration**: Next.js / React

---

## User Schema

This service defines **two distinct "user" concepts** — keep them straight.

### `IdentityUser` (this service)

`ApplicationUser : IdentityUser<Guid>` is the **auth identity**, stored in `AspNetUsers`. It owns login and credentials **only** — no org/profile data. Its `Id` is the canonical user id across the platform (the JWT subject).

| Column / Property       | Type   | Description                              |
| ----------------------- | ------ | ---------------------------------------- |
| `Id`                    | GUID   | Primary Key, unique identifier for user  |
| `UserName`              | string | Login username                           |
| `Email`                 | string | User email                               |
| `PasswordHash`          | string | Hashed password                          |
| `SecurityStamp`         | string | Used by Identity for security validation |

> Plus the standard Identity fields (`LockoutEnabled`, `AccessFailedCount`, …). `ApplicationUser` is intentionally **empty of domain fields**.

### `DomainUser` (the org-domain service)

The **org/business person** — name, tenant, role, department, employment — is **not** here. It lives in the [microservice](../employee-management-microservice/) as `DomainUser`, linked back to the identity via `DomainUser.IdentityUserId → AspNetUsers.Id` (a plain uuid, no cross-service FK). So `Department`, `JobTitle`, and manager relationships belong to the domain (`Employee`, `Manager`, `ReportingLine`) — **not** to `IdentityUser`.

---

## Roles

This microservice supports **role-based access control (RBAC)**:

| Role       | Description                                                               |
| ---------- | ------------------------------------------------------------------------- |
| `SysAdmin` | Full access across the system; can manage all data                        |
| `Admin`    | Manages Tenant, Company, and related data; Only scoped to an organization |
| `Manager`  | Can view and manage employees in their department/team                    |
| `Employee` | Regular user; can view and update their own profile and tasks assigned    |

> Roles are stored in the `AspNetRoles` table and linked to users via `AspNetUserRoles`.

---

## Endpoints (examples)

| Endpoint                 | Method       | Description                                      |
| ------------------------ | ------------ | ------------------------------------------------ |
| `/api/auth/register`     | POST         | Create a new user account                        |
| `/api/auth/login`        | POST         | Authenticate user, returns session/cookie or JWT |
| `/api/auth/logout`       | POST         | Logs out user                                    |
| `/api/auth/user/{id}`    | GET          | Returns the domain user (`DomainUser`) profile   |
| `/api/auth/identity/{id}`| GET          | Returns the identity (`ApplicationUser`) info    |
| `/api/roles`             | GET/POST/PUT | Manage roles (Admin/SysAdmin only)               |

> All endpoints are protected using **role-based authorization**.

---

## Integration

1. **Frontend (Next.js)**: Calls auth endpoints for login/logout and stores session via cookie or JWT.
2. **Employee Microservice**: Validates token or session cookie before allowing access to protected employee data.

---

## Setup & Run

1. Clone repo:

```bash
git clone https://github.com/yourusername/employee-management-identity.git
```

2. Configure `appsettings.json` with database connection.

````

3. Run the microservice:

```bash
dotnet run
````

4. Use Postman or frontend app to test registration and login.


# Remove the existing migration
dotnet ef migrations remove --project employee.management.identity.infrastructure --startup-project employee.management.identity

# Create a new one
dotnet ef migrations add IdentityAndLink --project employee.management.identity.infrastructure --startup-project employee.management.identity

# Apply it
dotnet ef database update --project employee.management.identity.infrastructure --startup-project employee.management.identity
