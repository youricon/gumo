import { FormEvent, useEffect, useState } from "react";

type ListResponse<T> = {
  items: T[];
  next_cursor: string | null;
};

type Game = {
  id: string;
  name: string;
  sorting_name: string | null;
  platforms: string[];
  description: string | null;
  release_date: string | null;
  genres: string[];
  developers: string[];
  publishers: string[];
  links: { name: string; url: string }[];
  visibility: "public" | "private";
  cover_image: string | null;
  background_image: string | null;
  icon: string | null;
  created_at: string;
  updated_at: string;
};

type GameVersion = {
  id: string;
  game_id: string;
  library_id: string;
  version_name: string;
  version_code: string | null;
  release_date: string | null;
  is_latest: boolean;
  notes: string | null;
  created_at: string;
  updated_at: string;
};

type Upload = {
  id: string;
  kind: string;
  library_id: string;
  platform: string;
  game_id: string | null;
  game_version_id: string | null;
  state: string;
  filename: string;
  declared_size_bytes: number;
  received_size_bytes: number;
  checksum: string | null;
  job_id: string | null;
  created_at: string;
  updated_at: string;
  expires_at: string | null;
  error: { code: string; message: string; retryable?: boolean | null } | null;
};

type Job = {
  id: string;
  kind: string;
  state: string;
  upload_id: string | null;
  game_id: string | null;
  game_version_id: string | null;
  progress: { phase: string; percent: number } | null;
  result: unknown;
  error: { code: string; message: string; retryable?: boolean | null } | null;
  created_at: string;
  updated_at: string;
};

type SaveSnapshot = {
  id: string;
  game_id: string;
  game_version_id: string;
  library_id: string;
  name: string;
  captured_at: string;
  archive_type: string;
  size_bytes: number;
  checksum: string;
  notes: string | null;
  created_at: string;
};

type AdminSession = {
  authenticated: boolean;
  mode: string;
  username: string | null;
};

type IntegrationToken = {
  id: string;
  label: string;
  enabled: boolean;
  created_at: string;
  updated_at: string;
};

type CreatedIntegrationToken = {
  token: IntegrationToken;
  plaintext_token: string;
};

type ApiErrorPayload = {
  error?: {
    code?: string;
    message?: string;
  };
};

type Route =
  | { kind: "catalog" }
  | { kind: "game"; gameId: string }
  | { kind: "admin" };

function parseRoute(pathname: string): Route {
  if (pathname === "/admin") {
    return { kind: "admin" };
  }
  const match = pathname.match(/^\/games\/([^/]+)$/);
  if (match) {
    return { kind: "game", gameId: decodeURIComponent(match[1]) };
  }
  return { kind: "catalog" };
}

function navigate(path: string) {
  window.history.pushState({}, "", path);
  window.dispatchEvent(new PopStateEvent("popstate"));
}

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
    ...init,
  });
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const payload = (await response.json()) as ApiErrorPayload;
      message = payload.error?.message ?? payload.error?.code ?? message;
    } catch {
      // Ignore invalid JSON error payloads.
    }
    throw new Error(message);
  }
  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

function toCommaSeparated(values: string[]) {
  return values.join(", ");
}

function parseCommaSeparated(value: string) {
  return value
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

function timestampLabel(value: string | null) {
  if (!value) {
    return "Unknown";
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function sizeLabel(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KiB`;
  }
  if (bytes < 1024 * 1024 * 1024) {
    return `${(bytes / (1024 * 1024)).toFixed(1)} MiB`;
  }
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GiB`;
}

function useRoute() {
  const [route, setRoute] = useState<Route>(() => parseRoute(window.location.pathname));

  useEffect(() => {
    const update = () => setRoute(parseRoute(window.location.pathname));
    window.addEventListener("popstate", update);
    return () => window.removeEventListener("popstate", update);
  }, []);

  return route;
}

function useCatalog() {
  const [games, setGames] = useState<Game[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    api<ListResponse<Game>>("/api/games")
      .then((response) => {
        if (cancelled) {
          return;
        }
        setGames(response.items);
        setError(null);
      })
      .catch((err: Error) => {
        if (cancelled) {
          return;
        }
        setError(err.message);
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return { games, loading, error };
}

function CatalogPage() {
  const { games, loading, error } = useCatalog();

  return (
    <section className="page-section">
      <div className="section-heading">
        <p className="eyebrow">Public Catalog</p>
        <h1>Managed archives with version history.</h1>
        <p className="lede">
          Gumo keeps playable versions and full save snapshots in one managed
          archive library, with Playnite acting as the primary desktop client.
        </p>
      </div>
      {loading ? <p className="muted">Loading catalog…</p> : null}
      {error ? <p className="error-banner">{error}</p> : null}
      <div className="catalog-grid">
        {games.map((game) => (
          <button
            key={game.id}
            className="game-card"
            onClick={() => navigate(`/games/${game.id}`)}
            type="button"
          >
            <div
              className="game-card-art"
              style={{
                backgroundImage: game.cover_image
                  ? `linear-gradient(rgba(12, 20, 17, 0.35), rgba(12, 20, 17, 0.7)), url(${game.cover_image})`
                  : undefined,
              }}
            />
            <div className="game-card-body">
              <div className="pill-row">
                {game.platforms.map((platform) => (
                  <span key={platform} className="pill">
                    {platform}
                  </span>
                ))}
              </div>
              <h2>{game.name}</h2>
              <p>{game.description ?? "No description yet."}</p>
              <span className="meta-line">
                Updated {timestampLabel(game.updated_at)}
              </span>
            </div>
          </button>
        ))}
      </div>
      {!loading && games.length === 0 ? (
        <p className="muted">No public games are available yet.</p>
      ) : null}
    </section>
  );
}

function GameDetailPage({ gameId }: { gameId: string }) {
  const [game, setGame] = useState<Game | null>(null);
  const [versions, setVersions] = useState<GameVersion[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    Promise.all([
      api<Game>(`/api/games/${gameId}`),
      api<ListResponse<GameVersion>>(`/api/games/${gameId}/versions`),
    ])
      .then(([gameResponse, versionResponse]) => {
        if (cancelled) {
          return;
        }
        setGame(gameResponse);
        setVersions(versionResponse.items);
        setError(null);
      })
      .catch((err: Error) => {
        if (cancelled) {
          return;
        }
        setError(err.message);
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [gameId]);

  if (loading) {
    return <section className="page-section"><p className="muted">Loading game…</p></section>;
  }
  if (error) {
    return <section className="page-section"><p className="error-banner">{error}</p></section>;
  }
  if (!game) {
    return <section className="page-section"><p className="muted">Game not found.</p></section>;
  }

  return (
    <section className="page-section">
      <button className="text-button" onClick={() => navigate("/")} type="button">
        Back to catalog
      </button>
      <div className="detail-hero">
        <div
          className="detail-art"
          style={{
            backgroundImage: game.background_image
              ? `linear-gradient(rgba(12, 20, 17, 0.3), rgba(12, 20, 17, 0.85)), url(${game.background_image})`
              : undefined,
          }}
        />
        <div className="detail-copy">
          <div className="pill-row">
            {game.platforms.map((platform) => (
              <span key={platform} className="pill">
                {platform}
              </span>
            ))}
          </div>
          <h1>{game.name}</h1>
          <p className="lede">{game.description ?? "No description yet."}</p>
          <dl className="detail-facts">
            <div>
              <dt>Release</dt>
              <dd>{game.release_date ?? "Unknown"}</dd>
            </div>
            <div>
              <dt>Genres</dt>
              <dd>{game.genres.join(", ") || "None"}</dd>
            </div>
            <div>
              <dt>Developers</dt>
              <dd>{game.developers.join(", ") || "None"}</dd>
            </div>
            <div>
              <dt>Publishers</dt>
              <dd>{game.publishers.join(", ") || "None"}</dd>
            </div>
          </dl>
          {game.links.length > 0 ? (
            <div className="link-row">
              {game.links.map((link) => (
                <a key={`${link.name}-${link.url}`} href={link.url} target="_blank" rel="noreferrer">
                  {link.name}
                </a>
              ))}
            </div>
          ) : null}
        </div>
      </div>

      <div className="panel">
        <div className="section-heading compact">
          <p className="eyebrow">Versions</p>
          <h2>Installable snapshots</h2>
        </div>
        <div className="table-list">
          {versions.map((version) => (
            <article key={version.id} className="table-row">
              <div>
                <strong>{version.version_name}</strong>
                <p>{version.notes ?? "No version notes."}</p>
              </div>
              <div className="row-meta">
                <span>{version.release_date ?? "Unknown release"}</span>
                {version.is_latest ? <span className="pill accent">latest</span> : null}
              </div>
            </article>
          ))}
          {versions.length === 0 ? <p className="muted">No versions have been imported yet.</p> : null}
        </div>
      </div>
    </section>
  );
}

function AdminPage() {
  const [session, setSession] = useState<AdminSession | null>(null);
  const [sessionLoading, setSessionLoading] = useState(true);
  const [sessionError, setSessionError] = useState<string | null>(null);
  const [password, setPassword] = useState("");

  const [games, setGames] = useState<Game[]>([]);
  const [uploads, setUploads] = useState<Upload[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [integrationTokens, setIntegrationTokens] = useState<IntegrationToken[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [selectedVersionId, setSelectedVersionId] = useState<string | null>(null);
  const [selectedGame, setSelectedGame] = useState<Game | null>(null);
  const [versions, setVersions] = useState<GameVersion[]>([]);
  const [saveSnapshots, setSaveSnapshots] = useState<SaveSnapshot[]>([]);
  const [adminError, setAdminError] = useState<string | null>(null);
  const [savingGame, setSavingGame] = useState(false);
  const [savingVersion, setSavingVersion] = useState(false);
  const [deletingGame, setDeletingGame] = useState(false);
  const [creatingToken, setCreatingToken] = useState(false);
  const [tokenLabel, setTokenLabel] = useState("");
  const [newPlaintextToken, setNewPlaintextToken] = useState<string | null>(null);

  const [gameForm, setGameForm] = useState({
    name: "",
    sorting_name: "",
    description: "",
    release_date: "",
    visibility: "private",
    genres: "",
    developers: "",
    publishers: "",
  });
  const [versionForm, setVersionForm] = useState({
    version_name: "",
    version_code: "",
    release_date: "",
    notes: "",
  });

  useEffect(() => {
    let cancelled = false;
    setSessionLoading(true);
    api<AdminSession>("/api/admin/session")
      .then((value) => {
        if (cancelled) {
          return;
        }
        setSession(value);
        setSessionError(null);
      })
      .catch((err: Error) => {
        if (cancelled) {
          return;
        }
        setSessionError(err.message);
      })
      .finally(() => {
        if (!cancelled) {
          setSessionLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!session?.authenticated) {
      return;
    }
    let cancelled = false;
    Promise.all([
      api<ListResponse<Game>>("/api/admin/games"),
      api<ListResponse<IntegrationToken>>("/api/admin/integration-tokens"),
      api<ListResponse<Upload>>("/api/admin/uploads?scope=recent"),
      api<ListResponse<Job>>("/api/admin/jobs?scope=recent"),
    ])
      .then(([gameResponse, tokenResponse, uploadResponse, jobResponse]) => {
        if (cancelled) {
          return;
        }
        setGames(gameResponse.items);
        setIntegrationTokens(tokenResponse.items);
        setUploads(uploadResponse.items);
        setJobs(jobResponse.items);
        setSelectedGameId((current) => current ?? gameResponse.items[0]?.id ?? null);
        setAdminError(null);
      })
      .catch((err: Error) => {
        if (cancelled) {
          return;
        }
        setAdminError(err.message);
      });

    return () => {
      cancelled = true;
    };
  }, [session?.authenticated]);

  useEffect(() => {
    if (!session?.authenticated || !selectedGameId) {
      setSelectedGame(null);
      setVersions([]);
      setSelectedVersionId(null);
      return;
    }
    let cancelled = false;
    Promise.all([
      api<Game>(`/api/admin/games/${selectedGameId}`),
      api<ListResponse<GameVersion>>(`/api/admin/games/${selectedGameId}/versions`),
    ])
      .then(([gameResponse, versionResponse]) => {
        if (cancelled) {
          return;
        }
        setSelectedGame(gameResponse);
        setVersions(versionResponse.items);
        setSelectedVersionId((current) => current ?? versionResponse.items[0]?.id ?? null);
        setGameForm({
          name: gameResponse.name,
          sorting_name: gameResponse.sorting_name ?? "",
          description: gameResponse.description ?? "",
          release_date: gameResponse.release_date ?? "",
          visibility: gameResponse.visibility,
          genres: toCommaSeparated(gameResponse.genres),
          developers: toCommaSeparated(gameResponse.developers),
          publishers: toCommaSeparated(gameResponse.publishers),
        });
      })
      .catch((err: Error) => {
        if (cancelled) {
          return;
        }
        setAdminError(err.message);
      });

    return () => {
      cancelled = true;
    };
  }, [selectedGameId, session?.authenticated]);

  useEffect(() => {
    if (!session?.authenticated || !selectedVersionId) {
      setSaveSnapshots([]);
      return;
    }
    const version = versions.find((item) => item.id === selectedVersionId);
    if (!version) {
      return;
    }
    setVersionForm({
      version_name: version.version_name,
      version_code: version.version_code ?? "",
      release_date: version.release_date ?? "",
      notes: version.notes ?? "",
    });
    let cancelled = false;
    api<ListResponse<SaveSnapshot>>(`/api/admin/versions/${selectedVersionId}/save-snapshots`)
      .then((response) => {
        if (!cancelled) {
          setSaveSnapshots(response.items);
        }
      })
      .catch((err: Error) => {
        if (!cancelled) {
          setAdminError(err.message);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [selectedVersionId, session?.authenticated, versions]);

  async function handleLogin(event: FormEvent) {
    event.preventDefault();
    setSessionError(null);
    try {
      const next = await api<AdminSession>("/api/admin/session/login", {
        method: "POST",
        body: JSON.stringify({ password }),
      });
      setSession(next);
      setPassword("");
    } catch (err) {
      setSessionError(err instanceof Error ? err.message : "Login failed");
    }
  }

  async function handleLogout() {
    const next = await api<AdminSession>("/api/admin/session/logout", {
      method: "POST",
      body: JSON.stringify({}),
    });
    setSession(next);
    setGames([]);
    setIntegrationTokens([]);
    setUploads([]);
    setJobs([]);
    setSelectedGameId(null);
    setSelectedVersionId(null);
    setNewPlaintextToken(null);
  }

  async function saveGame() {
    if (!selectedGameId) {
      return;
    }
    setSavingGame(true);
    setAdminError(null);
    try {
      const updated = await api<Game>(`/api/admin/games/${selectedGameId}`, {
        method: "PATCH",
        body: JSON.stringify({
          name: gameForm.name,
          sorting_name: gameForm.sorting_name || null,
          description: gameForm.description || null,
          release_date: gameForm.release_date || null,
          visibility: gameForm.visibility,
          genres: parseCommaSeparated(gameForm.genres),
          developers: parseCommaSeparated(gameForm.developers),
          publishers: parseCommaSeparated(gameForm.publishers),
        }),
      });
      setSelectedGame(updated);
      setGames((items) => items.map((item) => (item.id === updated.id ? updated : item)));
    } catch (err) {
      setAdminError(err instanceof Error ? err.message : "Failed to save game");
    } finally {
      setSavingGame(false);
    }
  }

  async function saveVersion() {
    if (!selectedVersionId) {
      return;
    }
    setSavingVersion(true);
    setAdminError(null);
    try {
      const updated = await api<GameVersion>(`/api/admin/versions/${selectedVersionId}`, {
        method: "PATCH",
        body: JSON.stringify({
          version_name: versionForm.version_name,
          version_code: versionForm.version_code || null,
          release_date: versionForm.release_date || null,
          notes: versionForm.notes || null,
        }),
      });
      setVersions((items) => items.map((item) => (item.id === updated.id ? updated : item)));
    } catch (err) {
      setAdminError(err instanceof Error ? err.message : "Failed to save version");
    } finally {
      setSavingVersion(false);
    }
  }

  async function deleteSelectedGame() {
    if (!selectedGameId || !selectedGame) {
      return;
    }

    const confirmed = window.confirm(
      `Delete '${selectedGame.name}' and all of its versions, uploads, and save snapshots?`,
    );
    if (!confirmed) {
      return;
    }

    setDeletingGame(true);
    setAdminError(null);
    try {
      await api<void>(`/api/admin/games/${selectedGameId}`, {
        method: "DELETE",
      });

      setGames((items) => {
        const remaining = items.filter((item) => item.id !== selectedGameId);
        const nextSelectedId = remaining[0]?.id ?? null;
        setSelectedGameId(nextSelectedId);
        return remaining;
      });
      setSelectedGame(null);
      setVersions([]);
      setSelectedVersionId(null);
      setSaveSnapshots([]);
    } catch (err) {
      setAdminError(err instanceof Error ? err.message : "Failed to delete game");
    } finally {
      setDeletingGame(false);
    }
  }

  async function createIntegrationToken(event: FormEvent) {
    event.preventDefault();
    setCreatingToken(true);
    setAdminError(null);
    try {
      const created = await api<CreatedIntegrationToken>("/api/admin/integration-tokens", {
        method: "POST",
        body: JSON.stringify({ label: tokenLabel }),
      });
      setIntegrationTokens((items) => [created.token, ...items]);
      setNewPlaintextToken(created.plaintext_token);
      setTokenLabel("");
    } catch (err) {
      setAdminError(err instanceof Error ? err.message : "Failed to create integration token");
    } finally {
      setCreatingToken(false);
    }
  }

  async function disableIntegrationToken(tokenId: string) {
    setAdminError(null);
    try {
      const disabled = await api<IntegrationToken>(
        `/api/admin/integration-tokens/${tokenId}/disable`,
        {
          method: "POST",
          body: JSON.stringify({}),
        },
      );
      setIntegrationTokens((items) =>
        items.map((item) => (item.id === disabled.id ? disabled : item)),
      );
    } catch (err) {
      setAdminError(err instanceof Error ? err.message : "Failed to disable integration token");
    }
  }

  async function deleteIntegrationToken(tokenId: string) {
    setAdminError(null);
    try {
      await api<void>(`/api/admin/integration-tokens/${tokenId}`, {
        method: "DELETE",
      });
      setIntegrationTokens((items) => items.filter((item) => item.id !== tokenId));
      setNewPlaintextToken((current) => current);
    } catch (err) {
      setAdminError(err instanceof Error ? err.message : "Failed to delete integration token");
    }
  }

  if (sessionLoading) {
    return <section className="page-section"><p className="muted">Checking admin session…</p></section>;
  }

  if (!session?.authenticated) {
    return (
      <section className="page-section admin-login">
        <div className="panel login-panel">
          <p className="eyebrow">Admin</p>
          <h1>Owner access</h1>
          <p className="lede">
            Use the configured local owner password to review metadata, uploads,
            jobs, and save snapshots.
          </p>
          <form className="stack" onSubmit={handleLogin}>
            <label className="field">
              <span>Password</span>
              <input
                autoComplete="current-password"
                onChange={(event) => setPassword(event.target.value)}
                type="password"
                value={password}
              />
            </label>
            <button className="primary-button" type="submit">
              Sign in
            </button>
          </form>
          {sessionError ? <p className="error-banner">{sessionError}</p> : null}
        </div>
      </section>
    );
  }

  const selectedVersion = versions.find((item) => item.id === selectedVersionId) ?? null;

  return (
    <section className="page-section admin-layout">
      <div className="admin-header">
        <div>
          <p className="eyebrow">Admin</p>
          <h1>Owner workflows</h1>
          <p className="lede">
            Signed in as {session.username ?? "owner"} using {session.mode} auth.
          </p>
        </div>
        <button className="text-button" onClick={handleLogout} type="button">
          Sign out
        </button>
      </div>

      {adminError ? <p className="error-banner">{adminError}</p> : null}

      <div className="admin-grid">
        <div className="panel sidebar-panel">
          <div className="section-heading compact">
            <p className="eyebrow">Games</p>
            <h2>Catalog records</h2>
          </div>
          <div className="table-list">
            {games.map((game) => (
              <button
                key={game.id}
                className={`table-row selectable ${game.id === selectedGameId ? "selected" : ""}`}
                onClick={() => setSelectedGameId(game.id)}
                type="button"
              >
                <div>
                  <strong>{game.name}</strong>
                  <p>{game.visibility}</p>
                </div>
                <span className="pill">{game.platforms.join(", ")}</span>
              </button>
            ))}
          </div>
        </div>

        <div className="panel">
          <div className="section-heading compact">
            <p className="eyebrow">Metadata</p>
            <h2>Game review</h2>
          </div>
          {selectedGame ? (
            <div className="stack">
              <div className="form-grid">
                <label className="field">
                  <span>Name</span>
                  <input
                    value={gameForm.name}
                    onChange={(event) =>
                      setGameForm((current) => ({ ...current, name: event.target.value }))
                    }
                  />
                </label>
                <label className="field">
                  <span>Sorting name</span>
                  <input
                    value={gameForm.sorting_name}
                    onChange={(event) =>
                      setGameForm((current) => ({
                        ...current,
                        sorting_name: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="field field-wide">
                  <span>Description</span>
                  <textarea
                    rows={5}
                    value={gameForm.description}
                    onChange={(event) =>
                      setGameForm((current) => ({
                        ...current,
                        description: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="field">
                  <span>Release date</span>
                  <input
                    placeholder="YYYY-MM-DD"
                    value={gameForm.release_date}
                    onChange={(event) =>
                      setGameForm((current) => ({
                        ...current,
                        release_date: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="field">
                  <span>Visibility</span>
                  <select
                    value={gameForm.visibility}
                    onChange={(event) =>
                      setGameForm((current) => ({
                        ...current,
                        visibility: event.target.value as "public" | "private",
                      }))
                    }
                  >
                    <option value="private">private</option>
                    <option value="public">public</option>
                  </select>
                </label>
                <label className="field">
                  <span>Genres</span>
                  <input
                    value={gameForm.genres}
                    onChange={(event) =>
                      setGameForm((current) => ({ ...current, genres: event.target.value }))
                    }
                  />
                </label>
                <label className="field">
                  <span>Developers</span>
                  <input
                    value={gameForm.developers}
                    onChange={(event) =>
                      setGameForm((current) => ({
                        ...current,
                        developers: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="field field-wide">
                  <span>Publishers</span>
                  <input
                    value={gameForm.publishers}
                    onChange={(event) =>
                      setGameForm((current) => ({
                        ...current,
                        publishers: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>
              <button className="primary-button" disabled={savingGame} onClick={saveGame} type="button">
                {savingGame ? "Saving…" : "Save game metadata"}
              </button>
              <button
                className="secondary-button destructive-button"
                disabled={deletingGame}
                onClick={deleteSelectedGame}
                type="button"
              >
                {deletingGame ? "Deleting…" : "Delete game"}
              </button>
            </div>
          ) : (
            <p className="muted">Select a game to edit its metadata.</p>
          )}
        </div>
      </div>

      <div className="admin-grid">
        <div className="panel">
          <div className="section-heading compact">
            <p className="eyebrow">Versions</p>
            <h2>Archive revisions</h2>
          </div>
          <div className="table-list">
            {versions.map((version) => (
              <button
                key={version.id}
                className={`table-row selectable ${version.id === selectedVersionId ? "selected" : ""}`}
                onClick={() => setSelectedVersionId(version.id)}
                type="button"
              >
                <div>
                  <strong>{version.version_name}</strong>
                  <p>{version.notes ?? "No notes"}</p>
                </div>
                <div className="row-meta">
                  {version.is_latest ? <span className="pill accent">latest</span> : null}
                  <span>{version.release_date ?? "Unknown"}</span>
                </div>
              </button>
            ))}
            {versions.length === 0 ? <p className="muted">No imported versions yet.</p> : null}
          </div>
          {selectedVersion ? (
            <div className="stack inset-top">
              <div className="form-grid">
                <label className="field">
                  <span>Version name</span>
                  <input
                    value={versionForm.version_name}
                    onChange={(event) =>
                      setVersionForm((current) => ({
                        ...current,
                        version_name: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="field">
                  <span>Version code</span>
                  <input
                    value={versionForm.version_code}
                    onChange={(event) =>
                      setVersionForm((current) => ({
                        ...current,
                        version_code: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="field">
                  <span>Release date</span>
                  <input
                    value={versionForm.release_date}
                    onChange={(event) =>
                      setVersionForm((current) => ({
                        ...current,
                        release_date: event.target.value,
                      }))
                    }
                  />
                </label>
                <label className="field field-wide">
                  <span>Notes</span>
                  <textarea
                    rows={4}
                    value={versionForm.notes}
                    onChange={(event) =>
                      setVersionForm((current) => ({
                        ...current,
                        notes: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>
              <button
                className="primary-button"
                disabled={savingVersion}
                onClick={saveVersion}
                type="button"
              >
                {savingVersion ? "Saving…" : "Save version metadata"}
              </button>
            </div>
          ) : null}
        </div>

        <div className="panel">
          <div className="section-heading compact">
            <p className="eyebrow">Save Snapshots</p>
            <h2>Version-tied backups</h2>
          </div>
          <div className="table-list">
            {saveSnapshots.map((snapshot) => (
              <article key={snapshot.id} className="table-row">
                <div>
                  <strong>{snapshot.name}</strong>
                  <p>{snapshot.notes ?? "No notes"}</p>
                </div>
                <div className="row-meta">
                  <span>{sizeLabel(snapshot.size_bytes)}</span>
                  <span>{timestampLabel(snapshot.captured_at)}</span>
                </div>
              </article>
            ))}
            {saveSnapshots.length === 0 ? (
              <p className="muted">No save snapshots for the selected version.</p>
            ) : null}
          </div>
        </div>
      </div>

      <div className="admin-grid">
        <div className="panel">
          <div className="section-heading compact">
            <p className="eyebrow">Integrations</p>
            <h2>API tokens</h2>
          </div>
          <form className="stack inset-bottom" onSubmit={createIntegrationToken}>
            <label className="field">
              <span>Label</span>
              <input
                placeholder="Playnite desktop"
                value={tokenLabel}
                onChange={(event) => setTokenLabel(event.target.value)}
              />
            </label>
            <button className="primary-button" disabled={creatingToken} type="submit">
              {creatingToken ? "Generating…" : "Generate new token"}
            </button>
          </form>
          {newPlaintextToken ? (
            <div className="panel inset-bottom">
              <p className="eyebrow">New token</p>
              <p className="muted">Copy it now. It will not be shown again.</p>
              <code>{newPlaintextToken}</code>
            </div>
          ) : null}
          <div className="table-list">
            {integrationTokens.map((token) => (
              <article key={token.id} className="table-row">
                <div>
                  <strong>{token.label}</strong>
                  <p>{token.enabled ? "enabled" : "disabled"}</p>
                </div>
                <div className="row-meta">
                  <span>{timestampLabel(token.created_at)}</span>
                  {token.enabled ? (
                    <>
                      <button
                        className="text-button"
                        onClick={() => disableIntegrationToken(token.id)}
                        type="button"
                      >
                        Disable
                      </button>
                      <button
                        className="text-button"
                        onClick={() => deleteIntegrationToken(token.id)}
                        type="button"
                      >
                        Delete
                      </button>
                    </>
                  ) : (
                    <>
                      <span className="pill">disabled</span>
                      <button
                        className="text-button"
                        onClick={() => deleteIntegrationToken(token.id)}
                        type="button"
                      >
                        Delete
                      </button>
                    </>
                  )}
                </div>
              </article>
            ))}
            {integrationTokens.length === 0 ? (
              <p className="muted">No integration tokens have been created yet.</p>
            ) : null}
          </div>
        </div>

        <div className="panel">
          <div className="section-heading compact">
            <p className="eyebrow">Uploads</p>
            <h2>Recent transfer state</h2>
          </div>
          <div className="table-list">
            {uploads.map((upload) => (
              <article key={upload.id} className="table-row">
                <div>
                  <strong>{upload.filename}</strong>
                  <p>{upload.kind} · {upload.state}</p>
                </div>
                <div className="row-meta">
                  <span>{sizeLabel(upload.received_size_bytes)}</span>
                  <span>{timestampLabel(upload.updated_at)}</span>
                </div>
              </article>
            ))}
            {uploads.length === 0 ? <p className="muted">No uploads yet.</p> : null}
          </div>
        </div>

        <div className="panel">
          <div className="section-heading compact">
            <p className="eyebrow">Jobs</p>
            <h2>Background processing</h2>
          </div>
          <div className="table-list">
            {jobs.map((job) => (
              <article key={job.id} className="table-row">
                <div>
                  <strong>{job.kind}</strong>
                  <p>{job.state}</p>
                </div>
                <div className="row-meta">
                  <span>
                    {job.progress ? `${job.progress.phase} ${job.progress.percent}%` : "waiting"}
                  </span>
                  <span>{timestampLabel(job.updated_at)}</span>
                </div>
              </article>
            ))}
            {jobs.length === 0 ? <p className="muted">No jobs yet.</p> : null}
          </div>
        </div>
      </div>
    </section>
  );
}

export function App() {
  const route = useRoute();

  return (
    <main className="app-shell">
      <header className="topbar">
        <button className="brand" onClick={() => navigate("/")} type="button">
          <span className="brand-mark">G</span>
          <span>Gumo</span>
        </button>
        <nav className="nav">
          <button
            className={route.kind === "catalog" ? "nav-link active" : "nav-link"}
            onClick={() => navigate("/")}
            type="button"
          >
            Catalog
          </button>
          <button
            className={route.kind === "admin" ? "nav-link active" : "nav-link"}
            onClick={() => navigate("/admin")}
            type="button"
          >
            Admin
          </button>
        </nav>
      </header>

      {route.kind === "catalog" ? <CatalogPage /> : null}
      {route.kind === "game" ? <GameDetailPage gameId={route.gameId} /> : null}
      {route.kind === "admin" ? <AdminPage /> : null}
    </main>
  );
}
