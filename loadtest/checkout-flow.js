/**
 * k6 Load Test — nopCommerce Checkout Flow (end-to-end)
 *
 * Drives the full "Customer places an order" user flow:
 *   1. Log in
 *   2. Add a product to cart
 *   3. Set checkout attributes (gift wrapping = No)
 *   4. Complete one-page checkout (billing, shipping, payment, confirm)
 *
 * Prerequisites:
 *   - nopCommerce running at BASE_URL with sample data installed
 *   - Admin account: admin@yourStore.com / admin
 *
 * Usage:
 *   k6 run loadtest/checkout-flow.js
 *   k6 run --vus 2 --duration 1m loadtest/checkout-flow.js
 */

import http from "k6/http";
import { check, sleep } from "k6";
import { Rate } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://localhost:80";
const EMAIL = __ENV.EMAIL || "admin@yourStore.com";
const PASSWORD = __ENV.PASSWORD || "admin";

// Simple products that can be added from catalog (no attribute selection required)
const SIMPLE_PRODUCT_IDS = [2, 3, 5, 18, 19, 20, 45];

export const options = {
  stages: [
    { duration: "15s", target: 1 },
    { duration: "2m", target: 1 },
    { duration: "15s", target: 0 },
  ],
  thresholds: {
    http_req_duration: ["p(95)<10000"],
  },
};

const checkoutSuccess = new Rate("checkout_completed");

function extractToken(body) {
  const m = (body || "").match(
    /name="__RequestVerificationToken"[^>]*value="([^"]+)"/
  );
  return m ? m[1] : "";
}

function ajaxHeaders(token) {
  return {
    "Content-Type": "application/x-www-form-urlencoded",
    "X-Requested-With": "XMLHttpRequest",
    RequestVerificationToken: token,
  };
}

export default function () {
  // --- 1. Log in ---
  const loginPage = http.get(`${BASE_URL}/login`, {
    tags: { name: "01_login_page" },
  });
  const loginToken = extractToken(loginPage.body);

  const loginRes = http.post(
    `${BASE_URL}/login`,
    {
      Email: EMAIL,
      Password: PASSWORD,
      __RequestVerificationToken: loginToken,
    },
    {
      tags: { name: "02_login" },
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      redirects: 5,
    }
  );
  if (
    !check(loginRes, {
      "logged in": (r) => r.body && r.body.includes("logout"),
    })
  ) {
    checkoutSuccess.add(0);
    return;
  }
  sleep(0.3);

  // --- 2. Get token from homepage and add a random simple product ---
  const homePage = http.get(`${BASE_URL}/`, { tags: { name: "03_homepage" } });
  const homeToken = extractToken(homePage.body);
  const productId =
    SIMPLE_PRODUCT_IDS[Math.floor(Math.random() * SIMPLE_PRODUCT_IDS.length)];

  const addRes = http.post(
    `${BASE_URL}/addproducttocart/catalog/${productId}/1/1`,
    null,
    {
      tags: { name: "04_add_to_cart" },
      headers: ajaxHeaders(homeToken),
    }
  );
  if (
    !check(addRes, {
      "added to cart": (r) => r.status === 200 && r.body && r.body.includes("success"),
    })
  ) {
    checkoutSuccess.add(0);
    return;
  }
  sleep(0.2);

  // --- 3. Set checkout attribute: gift wrapping = No ---
  const cartPage = http.get(`${BASE_URL}/cart`, {
    tags: { name: "05_cart" },
  });
  const cartToken = extractToken(cartPage.body);

  http.post(
    `${BASE_URL}/shoppingcart/checkoutattributechange/true`,
    { checkout_attribute_1: "1" },
    {
      tags: { name: "06_gift_wrap" },
      headers: ajaxHeaders(cartToken),
    }
  );
  sleep(0.2);

  // --- 4. Go to checkout ---
  const checkoutPage = http.get(`${BASE_URL}/onepagecheckout`, {
    tags: { name: "07_checkout" },
    redirects: 5,
  });
  if (
    !check(checkoutPage, {
      "checkout loaded": (r) => r.status === 200,
    })
  ) {
    checkoutSuccess.add(0);
    return;
  }
  const ct = extractToken(checkoutPage.body);
  sleep(0.2);

  // --- 5. Billing (new address) ---
  const billingRes = http.post(
    `${BASE_URL}/checkout/OpcSaveBilling`,
    {
      billing_address_id: "0",
      ShipToSameAddress: "true",
      "BillingNewAddress.FirstName": "Load",
      "BillingNewAddress.LastName": "Test",
      "BillingNewAddress.Email": EMAIL,
      "BillingNewAddress.CountryId": "1",
      "BillingNewAddress.StateProvinceId": "40",
      "BillingNewAddress.City": "New York",
      "BillingNewAddress.Address1": "123 Test St",
      "BillingNewAddress.ZipPostalCode": "10001",
      "BillingNewAddress.PhoneNumber": "5551234567",
    },
    { tags: { name: "08_billing" }, headers: ajaxHeaders(ct) }
  );
  if (!check(billingRes, { "billing ok": (r) => r.status === 200 })) {
    checkoutSuccess.add(0);
    return;
  }
  sleep(0.1);

  // --- 6. Shipping method ---
  http.post(
    `${BASE_URL}/checkout/OpcSaveShippingMethod`,
    { shippingoption: "Ground___Shipping.FixedByWeightByTotal" },
    { tags: { name: "09_shipping" }, headers: ajaxHeaders(ct) }
  );
  sleep(0.1);

  // --- 7. Payment method ---
  http.post(
    `${BASE_URL}/checkout/OpcSavePaymentMethod`,
    { paymentmethod: "Payments.CheckMoneyOrder" },
    { tags: { name: "10_payment" }, headers: ajaxHeaders(ct) }
  );
  sleep(0.1);

  // --- 8. Payment info ---
  http.post(`${BASE_URL}/checkout/OpcSavePaymentInfo`, null, {
    tags: { name: "11_payment_info" },
    headers: ajaxHeaders(ct),
  });
  sleep(0.1);

  // --- 9. Confirm order (triggers PlaceOrderAsync) ---
  const confirmRes = http.post(
    `${BASE_URL}/checkout/OpcConfirmOrder`,
    null,
    { tags: { name: "12_confirm" }, headers: ajaxHeaders(ct) }
  );

  if (confirmRes.body && !confirmRes.body.includes("success")) {
    console.log(`CONFIRM FAILED: ${(confirmRes.body || "").substring(0, 200)}`);
  }
  const placed = check(confirmRes, {
    "order placed": (r) =>
      r.status === 200 && r.body && r.body.includes("success"),
  });
  checkoutSuccess.add(placed ? 1 : 0);

  sleep(2);
}
