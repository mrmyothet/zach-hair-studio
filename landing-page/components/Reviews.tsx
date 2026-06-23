import { reviews } from "@/lib/data";
import SectionHeading from "./SectionHeading";
import { StarIcon } from "./icons";

export default function Reviews() {
  return (
    <section id="reviews" className="py-24 bg-charcoal">
      <div className="max-w-7xl mx-auto px-6">
        <SectionHeading
          eyebrow="Testimonials"
          title="Client"
          highlight="Reviews"
          subtitle="Don't just take our word for it — hear from the people who matter most."
        />

        <div className="grid md:grid-cols-3 gap-6">
          {reviews.map((review) => (
            <div
              key={review.name}
              className={`card-hover rounded-2xl p-7 flex flex-col h-full ${
                review.featured
                  ? "bg-gradient-to-br from-gold/10 to-transparent border border-gold/20"
                  : "bg-charcoal-light border border-white/5 hover:border-gold/30"
              }`}
            >
              <div className="flex gap-1 mb-4">
                {Array.from({ length: 5 }).map((_, i) => (
                  <StarIcon key={i} className="w-4 h-4 text-gold" />
                ))}
              </div>
              <p
                className={`text-sm leading-relaxed mb-6 italic ${
                  review.featured ? "text-gray-300" : "text-gray-400"
                }`}
              >
                &ldquo;{review.quote}&rdquo;
              </p>
              <div className="flex items-center gap-3 mt-auto">
                <div
                  className={`w-10 h-10 rounded-full flex items-center justify-center text-white font-semibold text-sm ${review.avatar}`}
                >
                  {review.initial}
                </div>
                <div>
                  <p className="text-white font-medium text-sm">{review.name}</p>
                  <p className="text-gray-500 text-xs">{review.role}</p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
