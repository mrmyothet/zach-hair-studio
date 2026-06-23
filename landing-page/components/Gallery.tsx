import Image from "next/image";
import { galleryItems } from "@/lib/data";
import SectionHeading from "./SectionHeading";

export default function Gallery() {
  return (
    <section id="gallery" className="py-24 bg-charcoal">
      <div className="max-w-7xl mx-auto px-6">
        <SectionHeading
          eyebrow="Our Work"
          title="Style"
          highlight="Gallery"
          subtitle="Each transformation tells a story. Browse our portfolio of stunning styles."
        />

        <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
          {galleryItems.map((item) => (
            <div
              key={item.src}
              className="group relative rounded-2xl overflow-hidden aspect-[3/4] card-hover cursor-pointer border border-white/5 hover:border-gold/30"
            >
              <Image
                src={item.src}
                alt={item.alt}
                fill
                sizes="(max-width: 768px) 50vw, 33vw"
                className={`object-cover ${item.position} transition-transform duration-500 group-hover:scale-105`}
              />
              <div className="absolute inset-0 bg-gradient-to-t from-charcoal/90 via-charcoal/20 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300 flex flex-col items-center justify-end pb-6 px-4">
                <p className="text-white font-semibold text-sm mb-1">{item.title}</p>
                <p className="text-gold text-xs uppercase tracking-wider">{item.tag}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
