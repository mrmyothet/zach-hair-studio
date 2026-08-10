import BackToTop from "@/components/BackToTop";
import CheckoutForm from "@/components/CheckoutForm";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";

export const metadata = {
  title: "Checkout | Zach Hair Studio",
  description: "Complete your guest checkout and continue to secure payment.",
};

// UI-SPEC in-flight CTA lives in CheckoutForm: "Redirecting to payment…"

export default function CheckoutPage() {
  return (
    <>
      <Navbar />
      <CheckoutForm />
      <Footer />
      <BackToTop />
    </>
  );
}
