use std::env;
use std::net::SocketAddr;
use std::path::PathBuf;

use anyhow::{Context, Result};
use gumo::api::routes;
use gumo::api::state::{frontend_router, AppState};
use gumo::config::AppConfig;
use gumo::db;
use gumo::upload_jobs;
use tower_http::trace::TraceLayer;
use tracing_subscriber::EnvFilter;

#[tokio::main]
async fn main() -> Result<()> {
    init_tracing();
    let config_path = parse_config_path(env::args().skip(1))?;
    let config =
        AppConfig::load_from_file(&config_path).with_context(|| "backend startup failed")?;
    let pool = db::connect_and_migrate(&config.storage)
        .await
        .with_context(|| "database initialization failed")?;
    db::sync_config_reference_data(&pool, &config)
        .await
        .with_context(|| "failed to sync config reference data")?;
    let state = AppState::new(config.clone(), pool);
    let _background_worker = upload_jobs::spawn_background_worker(state.clone());

    let mut app = routes::router(state).layer(TraceLayer::new_for_http());
    if let Some(frontend) = frontend_router() {
        app = app.merge(frontend);
    }

    let addr: SocketAddr = format!("{}:{}", config.server.listen_address, config.server.port)
        .parse()
        .context("invalid listen address")?;
    let listener = tokio::net::TcpListener::bind(addr)
        .await
        .with_context(|| format!("failed to bind {addr}"))?;

    tracing::info!(%addr, config = %config_path.display(), "gumo backend listening");
    axum::serve(listener, app)
        .await
        .context("http server exited unexpectedly")?;

    Ok(())
}

fn parse_config_path(args: impl Iterator<Item = String>) -> Result<PathBuf> {
    let mut args = args.peekable();

    while let Some(arg) = args.next() {
        if arg == "--config" {
            let value = args
                .next()
                .context("missing value after --config")?;
            return Ok(PathBuf::from(value));
        }
    }

    let env_path = env::var("GUMO_CONFIG_PATH").context(
        "missing --config argument and GUMO_CONFIG_PATH environment variable is not set",
    )?;
    Ok(PathBuf::from(env_path))
}

fn init_tracing() {
    let filter = EnvFilter::try_from_default_env()
        .unwrap_or_else(|_| EnvFilter::new("gumo=debug,tower_http=info"));
    tracing_subscriber::fmt().with_env_filter(filter).init();
}
