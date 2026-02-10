// Build script - FFmpeg 링킹은 나중에 추가

fn main() {
    // FFmpeg 라이브러리 링킹 (나중에 활성화)
    // println!("cargo:rustc-link-lib=avformat");
    // println!("cargo:rustc-link-lib=avcodec");
    // println!("cargo:rustc-link-lib=avutil");
    // println!("cargo:rustc-link-lib=swscale");
    // println!("cargo:rustc-link-lib=swresample");

    println!("cargo:rerun-if-changed=build.rs");
}
