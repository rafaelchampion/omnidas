# Pipeline de Desenvolvimento / Homologação

1. **Issue** com Contexto, Escopo, Critérios de Aceite e Dependências. Labels de status: `status: planejado` → `status: em andamento` → `status: em revisão` → fechada.
2. **Branch** `feature/issue-<ID>-<nome>`. Nunca commitar direto em `main` ou `dsv`.
3. **Homologação**: merge da feature em `dsv`, push (Vercel Preview quando aplicável ou build de CI), label `status: em revisão` e publicação do `### 🧪 Roteiro de Testes Manuais` na issue.
4. **Feedback**: o dono testa e roda `/test-feedback #ID`. Aprovado → merge em `main` e `Closes #ID`. Com falhas → correções na feature e novo ciclo. Novos requisitos → novas issues.

Nota: o Omnidas é um app desktop; a "preview" será um build assinado de teste gerado pelo CI na `dsv`.
