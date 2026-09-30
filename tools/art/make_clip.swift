// Joins numbered frames (the Mac player's -clip JPEGs) into a looping GIF for the README (29 Sep 2026).
//   swift tools/art/make_clip.swift <out.gif> <width> <fps> <folder> [<folder> ...]
// Folders are played one after another; every frame is scaled to <width> (height kept in proportion).
import AppKit
import ImageIO
import UniformTypeIdentifiers

let args = CommandLine.arguments
guard args.count >= 5, let width = Int(args[2]), let fps = Double(args[3]) else {
    print("usage: make_clip.swift out.gif width fps folder [folder ...]"); exit(1)
}
var frames: [URL] = []
for folder in args[4...] {
    let dir = URL(fileURLWithPath: folder)
    let files = (try? FileManager.default.contentsOfDirectory(at: dir, includingPropertiesForKeys: nil)) ?? []
    frames += files.filter { ["jpg", "png"].contains($0.pathExtension.lowercased()) }.sorted { $0.lastPathComponent < $1.lastPathComponent }
}
guard !frames.isEmpty,
      let dest = CGImageDestinationCreateWithURL(URL(fileURLWithPath: args[1]) as CFURL, UTType.gif.identifier as CFString, frames.count, nil)
else { print("no frames"); exit(1) }
CGImageDestinationSetProperties(dest, [kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFLoopCount: 0]] as CFDictionary)
let frameProps = [kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFDelayTime: 1.0 / fps]] as CFDictionary
for url in frames {
    guard let src = CGImageSourceCreateWithURL(url as CFURL, nil), let image = CGImageSourceCreateImageAtIndex(src, 0, nil) else { continue }
    let height = Int(Double(image.height) * Double(width) / Double(image.width))
    guard let ctx = CGContext(data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0,
                              space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue) else { continue }
    ctx.interpolationQuality = .high
    ctx.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
    if let scaled = ctx.makeImage() { CGImageDestinationAddImage(dest, scaled, frameProps) }
}
print(CGImageDestinationFinalize(dest) ? "wrote \(frames.count) frames to \(args[1])" : "failed")
