# FIAP X — AuthService

Microsserviço de autenticação em **.NET 8** com **ASP.NET Identity + JWT**.  
Arquitetura simples em **Clean Architecture** — **único projeto**, separado por pastas internas. Sem CQRS.

---

## Estrutura

```
AuthService/
├── src/
│   ├── Domain/
│   │   ├── Entities/          ApplicationUser.cs
│   │   └── Exceptions/        DomainException.cs
│   ├── Application/
│   │   ├── DTOs/              AuthDtos.cs
│   │   ├── Interfaces/        Interfaces.cs
│   │   └── Services/          AuthService.cs
│   ├── Infrastructure/
│   │   ├── Data/              AppDbContext.cs
│   │   ├── Jwt/               Jwt.cs (JwtSettings + JwtService)
│   │   └── Repositories/      UserRepository.cs
│   ├── API/
│   │   ├── Controllers/       AuthController.cs
│   │   └── Middlewares/       ExceptionMiddleware.cs
│   ├── Program.cs
│   └── AuthService.csproj
├── tests/
│   ├── Unit/Application/      AuthServiceTests.cs
│   ├── Integration/Controllers/ AuthControllerTests.cs
│   └── AuthService.Tests.csproj
├── k8s/
│   ├── 00-namespace-secret.yaml
│   ├── 01-postgres.yaml
│   └── 02-auth-service.yaml
├── Dockerfile
├── docker-compose.yml
└── .github/workflows/ci-cd.yml
```

---

## Rotas disponíveis

| Método | Rota                | Auth | Descrição           |
|--------|---------------------|------|---------------------|
| POST   | /api/auth/register  | ❌   | Registra usuário    |
| POST   | /api/auth/login     | ❌   | Autentica e retorna JWT |
| GET    | /health             | ❌   | Health check        |

---

## Executar localmente

```bash
# Docker Compose (sobe Postgres + API)
docker compose up --build

# Swagger UI
open http://localhost:5000/swagger
```

## Executar testes

```bash
cd AuthService

# Todos
dotnet test tests/

# Só unitários
dotnet test tests/ --filter "FullyQualifiedName~Unit"

# Só integração
dotnet test tests/ --filter "FullyQualifiedName~Integration"

# Com cobertura
dotnet test tests/ --collect:"XPlat Code Coverage"
```

---

## CI/CD

| Gatilho                     | Job              | Regra                                      |
|-----------------------------|------------------|--------------------------------------------|
| PR → `develop` ou `main`    | test             | Cobertura ≥ 80 % + PR aprovado (branch protection) |
| Push → `main`               | docker           | Build + push imagem para GHCR              |
| Push → `main`               | deploy           | `kubectl set image` no cluster             |

> **Branch protection** — ative em *Settings > Branches* para `develop`:  
> ✅ Require pull request reviews (1 aprovação)  
> ✅ Require status checks: `test`

---

## Variáveis necessárias no GitHub

| Secret          | Descrição                      |
|-----------------|--------------------------------|
| `KUBECONFIG`    | Kubeconfig do cluster          |
