---
trigger: always_on
---

# Rastreamento de Trabalho e Pipeline de Desenvolvimento / Homologação

Repositório: `omnidas` (use o `gh` CLI, autenticado com a conta `rafaelchampion`).
Referência completa da arquitetura: `/docs/PIPELINE_DEV_TESTES.md`.

**Toda entrega, plano e implementação deste projeto deve seguir rigorosamente este fluxo:**

## 1. Issues & Planejamento
- Toda entrega tem uma issue com: Contexto, Escopo, Critérios de Aceite e dependências (componentes, tokens ou documentação afetada).
- **Status por labels** (mantenha exatamente uma por issue):
  `status: planejado` → `status: em andamento` → `status: em revisão` → fechada.

## 2. Branches & Desenvolvimento
- Todo desenvolvimento é feito em branch de feature: `feature/issue-<ID>-<nome>`.
- Nunca commitar diretamente na branch principal (`main` / `master`) nem na branch de homologação (`dsv`).
- Ao iniciar a implementação: trocar label para `status: em andamento`.
- Conforme avançar, marcar os itens do checklist no corpo da issue.

## 3. Homologação & Preview Restrito (dsv)
- Ao concluir o código da feature:
  1. Fazer merge da feature na branch `dsv` e push para acionar a Vercel Preview.
  2. O ambiente preview é acessível para validação interna (com proteção de homologação via Middleware ou Vercel Deployment Protection quando habilitado).
  3. Trocar label para `status: em revisão`.
  4. Publicar na issue do GitHub (e no chat) a seção `### 🧪 Roteiro de Testes Manuais` com checklist interativo (responsividade mobile/desktop, variações de tema light/dark, estados dos componentes e acessibilidade) e o link da Preview.

## 4. Feedback do Dono & Ciclo de Decisão
- O dono testa no celular e/ou desktop e roda `/test-feedback #ID` (ou diz "testei a issue #ID").
- O agente utiliza perguntas interativas no chat (`ask_question`) item por item para registrar o resultado.
- O agente publica o laudo na issue do GitHub:
  - **Se 100% aprovado**: merge da branch da feature para a branch principal (`main` / `master` - produção), push, e fechamento da issue (`Closes #ID`).
  - **Se houver ressalvas/falhas**: volta para a branch da feature, executa as correções, atualiza a `dsv` e solicita novo teste.
  - **Se surgirem novos requisitos não planejados**: abre novas issues filhas/relacionadas.

