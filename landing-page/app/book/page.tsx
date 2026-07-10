import AppointmentBookingForm from "@/components/AppointmentBookingForm";
import BackToTop from "@/components/BackToTop";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";
import SectionHeading from "@/components/SectionHeading";
import { fetchStylists } from "@/lib/appointments";
import { fetchServices } from "@/lib/services";

type Props = {
  searchParams: Promise<{ service?: string }>;
};

export default async function BookPage({ searchParams }: Props) {
  const [{ service }, services, stylists] = await Promise.all([
    searchParams,
    fetchServices(),
    fetchStylists(),
  ]);

  return (
    <>
      <Navbar />
      <main className="min-h-screen bg-charcoal-light pt-32">
        <section className="py-16">
          <div className="max-w-4xl mx-auto px-6">
            <SectionHeading
              eyebrow="Book A Service"
              title="Book Your"
              highlight="Appointment"
              subtitle="Pick a service, choose an open time, and confirm — your appointment is reserved the moment you submit."
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
              <AppointmentBookingForm
                services={services}
                stylists={stylists}
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
