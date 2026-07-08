import BackToTop from "@/components/BackToTop";
import BookingRequestForm from "@/components/BookingRequestForm";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import SectionHeading from "@/components/SectionHeading";
import { fetchServices } from "@/lib/services";

type Props = {
  searchParams: Promise<{ service?: string }>;
};

export default async function BookPage({ searchParams }: Props) {
  const [{ service }, services] = await Promise.all([
    searchParams,
    fetchServices(),
  ]);

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-4xl mx-auto px-6">
            <SectionHeading
              eyebrow="Book A Service"
              title="Request Your"
              highlight="Appointment"
              subtitle="Choose your service and preferred date. We'll confirm the appointment details with you directly."
            />

            {services.length === 0 ? (
              <div className="bg-charcoal border border-white/5 rounded-3xl p-8 text-center">
                <h1 className="text-white text-2xl font-serif mb-3">
                  Services are unavailable
                </h1>
                <p className="text-gray-400 text-sm leading-relaxed">
                  We could not load the service catalog right now. Please try
                  again shortly or contact the studio directly.
                </p>
              </div>
            ) : (
              <BookingRequestForm
                services={services}
                initialServiceSlug={service}
              />
            )}
          </div>
        </section>
      </main>
      <Footer />
      <BackToTop />
    </>
  );
}
