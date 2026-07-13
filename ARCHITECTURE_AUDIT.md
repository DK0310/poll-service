# ARCHITECTURE.md ↔ Codebase Alignment Audit

**Date:** 2026-07-13 · **Doc audited:** [ARCHITECTURE.md](ARCHITECTURE.md)
**Trigger:** the **multi-question survey refactor** — the data model changed from "a poll IS one
question with options" to the teacher's spec: **1 Poll has many Questions; each Question has many
Options.** This audit records the doc↔code sync done as part of that refactor.

**Verdict:** ARCHITECTURE.md was updated in lockstep with the code and now matches it. All three
backend test suites and the frontend build pass.

---

## What changed (code) and how the doc was synced

| Area | Code change | Doc updated |
|---|---|---|
| **poll-api schema** | New `Question` entity between `Poll` and `PollOption`; `Poll` drops `Question`/`Type`, gains `Title` + `Questions`; `PollOption.PollId`→`QuestionId`; `PollQuestionType` moved to `Question.cs`. Fresh `InitialCreate` migration. | Schema diagram, Entities (Poll/Question/PollOption), Indexes, DB-per-service table |
| **poll-api API** | `CreatePollRequest` → `{ title?, questions:[{text,type,options}], expiryHours? }`; `PollResponse` nests `questions[]`; `PollService.CreateAsync` loops questions reusing `BuildOptionTexts`. | Poll API endpoints table, System Overview, Data Flows |
| **vote-api model** | `Vote` gains `QuestionId`; unique index → `(PollCode, QuestionId, VoterToken)`; aggregation index → `(PollCode, QuestionId, OptionIndex)`. | Vote entity, Indexes, Cross-Service References |
| **vote-api batch + per-question** | `VoteRequest` batch (`answers[]`); `VoteResultsResponse`/`AnalyticsResponse` per-question; `PollClientService.PollInfo` mirrors nested poll; validate whole batch → one `AddRange`. Fresh migration. | Vote API endpoints, Data Flows (SignalR/batch), Design Decisions |
| **audience Q&A → Ask rename** | `Question`/`QuestionUpvote`→`AudienceQuestion`/`AudienceQuestionUpvote`; `QuestionService`→`AskService`; `QuestionsController`→`AskController`; routes `/questions`→`/ask`; SignalR `ReceiveQuestionsUpdate`→`ReceiveAskUpdate`; DbSets/tables/index renamed. | Microservices table, tree, entities, endpoints, gateway route, RBAC, Design Decisions |
| **gateway** | Route `vote-questions` → `vote-ask` (`/api/polls/{code}/ask/{**}`); survey questions ride the `polls-public` catch-all (embedded in the poll payload). | Gateway Routing Table |
| **frontend** | Types nested; `PollForm` multi-question builder; new `SurveyForm`; `VoteForm` controlled per-question; batch `useVote`; per-question `ResultsPage`/`AnalyticsPage`; `QandAPanel`/`useQuestions`→`AskPanel`/`useAsk`; `PollCard`/pages show title + question count. | Project Structure (frontend), component/hook descriptions |

---

## Verification (this refactor)

| Check | Result |
|---|---|
| `dotnet test services/poll-api/PollApi.sln` | **39 passed** (added multi-question + per-question option-validation cases) |
| `dotnet test services/vote-api/VoteApi.sln` | **48 passed** (batch, per-question aggregation, invalid/missing-answer, Ask rename) |
| poll-api / vote-api fresh `InitialCreate` migrations | regenerated; `Polls→Questions→PollOptions` FK chain + `Votes.QuestionId` composite indexes + renamed `AudienceQuestions*` tables verified |
| `npm run lint` (frontend) | clean |
| `npm run build` (frontend, tsc + vite) | green |
| Gateway `appsettings.json` | valid JSON; no route still sends `/questions` to vote-api |

---

## Open assumptions (see KNOWN_ISSUES.md "Design notes")

- No survey-edit endpoint → `QuestionId` is immutable for a poll's lifetime (Vote API trusts the id
  learned from the Poll API).
- Batch voting requires an answer to **every** question (no partial submission), keeping `TotalVoters`
  equal to each question's vote count.

> **Prior audit (2026-07-03, HEAD `148c6ab`):** found and fixed the unset `GATEWAY_URL` breaking the
> compose frontend proxy, plus two minor doc gaps. Superseded by this record.
