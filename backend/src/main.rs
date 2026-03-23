use std::env;
use std::path::PathBuf;

use anyhow::{Context, Result};
use gumo::config::AppConfig;
use gumo::db;

#[tokio::main]
async fn main() -> Result<()> {
    let config_path = parse_config_path(env::args().skip(1))?;
    let config =
        AppConfig::load_from_file(&config_path).with_context(|| "backend startup failed")?;
    let pool = db::connect_and_migrate(&config.storage)
        .await
        .with_context(|| "database initialization failed")?;
    let foreign_keys_enabled = db::foreign_keys_enabled(&pool).await?;

    println!("gumo backend scaffold");
    println!("config={}", config_path.display());
    println!(
        "libraries={} platforms={} playnite_enabled={} foreign_keys={}",
        config.libraries.len(),
        config.platforms.len(),
        config.integrations.playnite.enabled,
        foreign_keys_enabled
    );

    pool.close().await;

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
