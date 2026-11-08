import AppKit
import Foundation

let destination = URL(fileURLWithPath: CommandLine.arguments[1])
try FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true)
for size in [16, 32, 128, 256, 512] {
    for scale in [1, 2] {
        let pixels = size * scale
        let image = NSImage(size: NSSize(width: pixels, height: pixels))
        image.lockFocus()
        NSColor(calibratedRed: 153/255, green: 44/255, blue: 73/255, alpha: 1).setFill()
        let unit = CGFloat(pixels) / 1024
        NSBezierPath(roundedRect: NSRect(x: 40*unit, y: 40*unit, width: 944*unit, height: 944*unit), xRadius: 210*unit, yRadius: 210*unit).fill()
        NSColor.white.setFill()
        NSBezierPath(roundedRect: NSRect(x: 240*unit, y: 320*unit, width: 544*unit, height: 440*unit), xRadius: 100*unit, yRadius: 100*unit).fill()
        let tail = NSBezierPath(); tail.move(to: NSPoint(x: 340*unit, y: 370*unit))
        tail.line(to: NSPoint(x: 330*unit, y: 220*unit)); tail.line(to: NSPoint(x: 510*unit, y: 370*unit)); tail.close(); tail.fill()
        image.unlockFocus()
        let bitmap = NSBitmapImageRep(data: image.tiffRepresentation!)!
        let name = "icon_\(size)x\(size)\(scale == 2 ? "@2x" : "").png"
        try bitmap.representation(using: .png, properties: [:])!.write(to: destination.appendingPathComponent(name))
    }
}
