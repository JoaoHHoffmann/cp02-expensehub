# Checkpoint 2 — ExpenseHub

Checkpoint de C# em grupos de até 3 pessoas para construção de uma Application Programming Interface (API) corporativa de reembolsos.

O prazo de entrega é **13 de outubro de 2026**. O grupo deverá implementar autenticação, autorização, fluxo de aprovação e reprovação, pagamento simulado, histórico e testes unitários.

## Criar seu repositório

1. Clique em **Use this template**.
2. Selecione **Create a new repository**.
3. Crie um repositório **público** em uma das contas do grupo.
4. Adicione os demais integrantes como colaboradores.
5. Clone o repositório.

Não use fork. As issues permanecem neste repositório original como especificação comum da turma.

Os commits serão utilizados para avaliar a participação. Todos os membros do grupo
devem possuir mais de um commit no repositório.

## Fluxo de trabalho

Para cada issue:

1. leia os critérios no repositório original;
2. crie uma branch com o identificador, por exemplo `i06-ownership`;
3. implemente e valide a feature;
4. abra uma pull request no seu próprio repositório;
5. use um título como `I06 — Ownership e matriz de acesso`;
6. adicione na descrição uma referência completa, como `Racass/checkpoint-csharpracass-expensehub#6`;
7. não use `Closes`, `Fixes` ou `Resolves`, pois a issue original deve permanecer aberta;
8. conclua a auto-revisão e faça o merge.

## Estrutura inicial

```text
sources/
├── ExpenseHub.slnx
├── ExpenseHub.Api/
└── ExpenseHub.UnitTests/
```

A solução começa sem Identity, banco, domínio ou testes funcionais. Toda implementação avaliada deve ser criada por você.

## Comandos

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

O endpoint inicial `GET /health` existe apenas para confirmar que a aplicação inicia.

## Documentação

- [Enunciado](docs/ENUNCIADO.md)
- [Requisitos e contratos](docs/REQUISITOS.md)
- [Rubrica](docs/RUBRICA.md)
- [Matriz de autorização](docs/MATRIZ-AUTORIZACAO.md)
- [Processo no GitHub](docs/PROCESSO-GITHUB.md)
- [Uso de Inteligência Artificial](docs/USO-DE-IA.md)
- [Regras do pipeline de qualidade](docs/code-quality-rules.md)

## Banco de dados

Você pode utilizar Microsoft SQL Server LocalDB, Oracle Database, SQLite ou outro provider relacional compatível com Entity Framework Core.

A escolha não gera pontos. Documente no README do seu repositório:

- provider e pacote utilizado;
- configuração necessária;
- criação ou atualização do banco;
- como iniciar a aplicação.

Não versione senhas, tokens ou connection strings sensíveis.

## Testes

Somente testes unitários escritos por você entram na nota. Testes de integração, end-to-end ou de interface são permitidos, mas opcionais e sem pontuação.

Os testes unitários devem executar sem banco, rede ou serviço externo.

## Entrega

Entregue:

- URL do repositório público;
- commit Secure Hash Algorithm (SHA) final;
- integração contínua executada;
- documentação atualizada.

O projeto deve compilar sem erros e ser entregue sem warnings para receber a pontuação integral de Qualidade de Código.


## Configuração do grupo

### Banco de dados

- **Provider:** SQLite, pacote `Microsoft.EntityFrameworkCore.Sqlite`.
- **Configuração:** a connection string `ConnectionStrings:ExpenseHub` fica em
  `sources/ExpenseHub.Api/appsettings.json` (`Data Source=expensehub.db`). O arquivo
  do banco é criado na pasta onde a aplicação é executada e não é versionado.
- **Criação e atualização:** as migrations são aplicadas automaticamente quando a
  aplicação inicia. Para aplicar manualmente:

```shell
dotnet tool install --global dotnet-ef
dotnet ef database update --project sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

### Como executar

```shell
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

A API sobe em `http://localhost:5245`. Teste com `GET /health`.


### Conta Admin inicial

Na inicialização, o seed cria as roles `Admin`, `Employee`, `Approver`, `Finance` e
`Auditor` e uma única conta Admin. O e-mail fica em `appsettings.json`
(`Seed:AdminEmail`). A senha **não é versionada** e precisa ser configurada antes
de iniciar a aplicação:

```shell
dotnet user-secrets set "Seed:AdminPassword" "<senha>" --project sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Também é possível usar a variável de ambiente `Seed__AdminPassword`. A senha precisa
ter ao menos 6 caracteres, com maiúscula, minúscula, número e símbolo.

### Autenticação

- `POST /register` com `{ "email": "...", "password": "..." }` cria um usuário sem role.
- `POST /login` com o mesmo corpo devolve um `accessToken`.
- Nas rotas protegidas, envie o header `Authorization: Bearer <accessToken>`.

### Administração de usuários (somente Admin)

- `GET /api/admin/users` lista os usuários e suas roles.
- `PUT /api/admin/users/{id}/roles` com `{ "roles": ["Employee", "Approver"] }` define
  as roles do usuário. Só aceita roles conhecidas e impede que o Admin remova a própria
  role `Admin`.
- Depois de uma alteração, os tokens antigos do usuário deixam de valer e ele precisa
  fazer login de novo.

  ### Reembolsos

Categorias disponíveis (`categoryId`): 1 Alimentação, 2 Transporte, 3 Hospedagem e 4 Outros.

- `POST /api/expenses` (role `Employee`) cria um rascunho:
  `{ "categoryId": 1, "description": "Almoço com cliente", "amount": 50.00, "expenseDate": "2026-10-01" }`
- `PUT /api/expenses/{id}` (somente o proprietário, e somente em `Draft`) edita o rascunho com o mesmo corpo.

O proprietário, o estado e os horários são definidos pelo servidor. Campos extras enviados
pelo cliente, como `status` ou `ownerId`, são ignorados.