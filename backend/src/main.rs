use std::env;

fn main() {
    let args: Vec<String> = env::args().collect();
    println!("gumo backend scaffold");
    println!("args: {:?}", args);
}
