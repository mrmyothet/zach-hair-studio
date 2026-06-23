import { services } from "@/lib/data";
import SectionHeading from "./SectionHeading";

export default function Services() {
  return (
    <section id="services" className="py-24 bg-charcoal-light">
      <div className="max-w-7xl mx-auto px-6">
        <SectionHeading
          eyebrow="What We Offer"
          title="Our"
          highlight="Services"
          subtitle="From everyday cuts to full transformations, our expert stylists deliver results that speak for themselves."
        />

        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
          {services.map((service) => (
            <div
              key={service.title}
              className="card-hover bg-charcoal border border-white/5 hover:border-gold/30 rounded-2xl p-7 group"
            >
              <div className="w-14 h-14 bg-gold/10 group-hover:bg-gold/20 rounded-xl flex items-center justify-center mb-5 transition-colors">
                <svg
                  className="w-7 h-7 text-gold"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth={1.5}
                  viewBox="0 0 24 24"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" d={service.icon} />
                </svg>
              </div>
              <h3 className="text-white text-lg font-semibold mb-2">{service.title}</h3>
              <p className="text-gray-500 text-sm leading-relaxed mb-5">{service.description}</p>
              <div className="flex items-center justify-between">
                <span className="text-gold font-bold text-lg">
                  {service.price}
                  <span className="text-gray-500 text-sm font-normal"> / session</span>
                </span>
                <a
                  href="#contact"
                  className="text-gold text-xs uppercase tracking-wider hover:underline"
                >
                  Book &rarr;
                </a>
              </div>
            </div>
          ))}

          <div className="card-hover bg-gradient-to-br from-gold/20 to-gold/5 border border-gold/30 rounded-2xl p-7 flex flex-col justify-between">
            <div>
              <span className="inline-block bg-gold text-charcoal text-xs font-bold uppercase tracking-wider px-3 py-1 rounded-full mb-5">
                Best Value
              </span>
              <h3 className="text-white text-lg font-semibold mb-2">Full Glam Package</h3>
              <p className="text-gray-400 text-sm leading-relaxed mb-5">
                Cut + Color + Blowout + Scalp treatment. The complete studio experience in one
                visit.
              </p>
            </div>
            <div className="flex items-center justify-between">
              <div>
                <span className="text-gray-500 text-sm line-through">$280</span>
                <span className="text-gold font-bold text-2xl ml-2">$199</span>
              </div>
              <a
                href="#contact"
                className="bg-gold hover:bg-gold-dark text-charcoal text-xs font-bold uppercase tracking-wider px-4 py-2 rounded-full transition-colors"
              >
                Book &rarr;
              </a>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
